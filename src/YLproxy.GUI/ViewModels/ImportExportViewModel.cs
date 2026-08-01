using System;
using System.IO;
using System.Text.Json;
using YLproxy.Infrastructure;
using YLproxy.Models;
using WinForms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace YLproxy.GUI.ViewModels;

public interface IFileDialogService
{
    string? ShowSaveJsonPath(string? initialDirectory);
    string? ShowOpenJsonPath(string? initialDirectory);
}

public interface IUserNotificationService
{
    void ShowInfo(string message, string title);
    void ShowError(string message, string title);
}

internal sealed class WinFormsFileDialogService : IFileDialogService
{
    public string? ShowSaveJsonPath(string? initialDirectory)
    {
        var saveFileDialog = new WinForms.SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = "proxies_export.json",
            InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        return saveFileDialog.ShowDialog() == WinForms.DialogResult.OK
            ? saveFileDialog.FileName
            : null;
    }

    public string? ShowOpenJsonPath(string? initialDirectory)
    {
        var openFileDialog = new WinForms.OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        return openFileDialog.ShowDialog() == WinForms.DialogResult.OK
            ? openFileDialog.FileName
            : null;
    }
}

internal sealed class MessageBoxNotificationService : IUserNotificationService
{
    public void ShowInfo(string message, string title)
    {
        MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    public void ShowError(string message, string title)
    {
        MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }
}

/// <summary>
/// 负责导入导出功能
/// </summary>
public sealed class ImportExportViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private readonly IFileDialogService _fileDialogService;
    private readonly IUserNotificationService _notificationService;
    private bool _isExporting;
    private bool _isImporting;

    public bool IsExporting
    {
        get => _isExporting;
        private set => SetProperty(ref _isExporting, value);
    }

    public bool IsImporting
    {
        get => _isImporting;
        private set => SetProperty(ref _isImporting, value);
    }

    public ImportExportViewModel(
        ILogger logger,
        IFileDialogService? fileDialogService = null,
        IUserNotificationService? notificationService = null)
    {
        _logger = logger;
        _fileDialogService = fileDialogService ?? new WinFormsFileDialogService();
        _notificationService = notificationService ?? new MessageBoxNotificationService();
    }

    /// <summary>
    /// 导出代理到JSON文件
    /// </summary>
    public void ExportToJson(List<ProxyItem> proxies, string? initialDirectory = null)
    {
        if (IsExporting)
            return;

        IsExporting = true;
        try
        {
        if (proxies == null || proxies.Count == 0)
        {
            _notificationService.ShowInfo("没有可导出的代理。", "导出");
            return;
        }

        var targetPath = _fileDialogService.ShowSaveJsonPath(initialDirectory);
        if (string.IsNullOrWhiteSpace(targetPath))
            return;

            var exportData = new
            {
                Proxies = proxies.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.RemoteHost,
                    p.RemotePort,
                    p.Username,
                    p.Password,
                    p.Group,
                    p.LocalHost,
                    p.LocalPort,
                    p.Status,
                    p.CreateTime
                })
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(targetPath, json);

            _notificationService.ShowInfo($"成功导出 {proxies.Count} 个代理到 {Path.GetFileName(targetPath)}", "导出成功");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"导出失败: {ex.Message}", "导出错误");
            _logger.Error($"Export failed: {ex.Message}");
        }
        finally
        {
            IsExporting = false;
        }
    }

    /// <summary>
    /// 从JSON文件导入代理
    /// </summary>
    public void ImportFromJson(List<ProxyItem> proxiesCollection, string? initialDirectory = null)
    {
        if (IsImporting)
            return;

        IsImporting = true;
        try
        {
            var sourcePath = _fileDialogService.ShowOpenJsonPath(initialDirectory);
            if (string.IsNullOrWhiteSpace(sourcePath))
                return;

            var json = File.ReadAllText(sourcePath);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("Proxies", out var proxiesEl) ||
                proxiesEl.ValueKind != JsonValueKind.Array)
            {
                _notificationService.ShowError("无效的导出文件: 缺少 'Proxies' 数组。", "导入错误");
                return;
            }

            var imported = 0;
            foreach (var proxyEl in proxiesEl.EnumerateArray())
            {
                try
                {
                    var name = proxyEl.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var host = proxyEl.TryGetProperty("RemoteHost", out var rh) ? rh.GetString() ?? "" : "";
                    var port = proxyEl.TryGetProperty("RemotePort", out var rp) ? rp.GetInt32() : 0;
                    var group = proxyEl.TryGetProperty("Group", out var gr) ? gr.GetString() ?? "" : "";

                    if (string.IsNullOrWhiteSpace(host) || port <= 0) continue;

                    // 简单的端口冲突检查（实际应用中可能需要更复杂的逻辑）
                    var localPort = 9001; // 起始端口
                    while (proxiesCollection.Any(p => p.LocalPort == localPort))
                    {
                        localPort++;
                        if (localPort > 9999) break; // 结束端口
                    }

                    if (localPort > 9999)
                    {
                        _notificationService.ShowError("无法分配本地端口，端口范围已用完。", "导入错误");
                        break;
                    }

                    proxiesCollection.Add(new ProxyItem
                    {
                        Id = proxiesCollection.Count > 0 ? proxiesCollection.Max(p => p.Id) + 1 : 1,
                        Name = name,
                        RemoteHost = host,
                        RemotePort = port,
                        Username = "",
                        Password = "",
                        Group = group,
                        LocalHost = "127.0.0.1",
                        LocalPort = localPort,
                        Status = ProxyStatus.Stopped,
                        CreateTime = DateTime.UtcNow
                    });

                    imported++;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Skipped invalid proxy entry during import: {ex.Message}");
                }
            }

            _notificationService.ShowInfo($"成功导入 {imported} 个代理从 {Path.GetFileName(sourcePath)}", "导入成功");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"导入失败: {ex.Message}", "导入错误");
            _logger.Error($"Import failed: {ex.Message}");
        }
        finally
        {
            IsImporting = false;
        }
    }
}
