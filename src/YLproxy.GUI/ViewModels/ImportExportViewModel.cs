using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using YLproxy.Models;
using YLproxy.Infrastructure;

namespace YLproxy.GUI.ViewModels;

/// <summary>
/// 负责导入导出功能
/// </summary>
public sealed class ImportExportViewModel : ViewModelBase
{
    private readonly ILogger _logger;

    public ImportExportViewModel(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 导出代理到JSON文件
    /// </summary>
    public void ExportToJson(List<ProxyItem> proxies, string? initialDirectory = null)
    {
        if (proxies == null || proxies.Count == 0)
        {
            MessageBox.Show("没有可导出的代理。", "导出", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = "proxies_export.json",
            InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (saveFileDialog.ShowDialog() != true)
            return;

        try
        {
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
            File.WriteAllText(saveFileDialog.FileName, json);

            MessageBox.Show($"成功导出 {proxies.Count} 个代理到 {Path.GetFileName(saveFileDialog.FileName)}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败: {ex.Message}", "导出错误", MessageBoxButton.OK, MessageBoxImage.Error);
            _logger.Error($"Export failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 从JSON文件导入代理
    /// </summary>
    public void ImportFromJson(List<ProxyItem> proxiesCollection, string? initialDirectory = null)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (openFileDialog.ShowDialog() != true)
            return;

        try
        {
            var json = File.ReadAllText(openFileDialog.FileName);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("Proxies", out var proxiesEl) ||
                proxiesEl.ValueKind != JsonValueKind.Array)
            {
                MessageBox.Show("无效的导出文件: 缺少 'Proxies' 数组。", "导入错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show("无法分配本地端口，端口范围已用完。", "导入错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

            MessageBox.Show($"成功导入 {imported} 个代理从 {Path.GetFileName(openFileDialog.FileName)}", "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败: {ex.Message}", "导入错误", MessageBoxButton.OK, MessageBoxImage.Error);
            _logger.Error($"Import failed: {ex.Message}");
        }
    }
}