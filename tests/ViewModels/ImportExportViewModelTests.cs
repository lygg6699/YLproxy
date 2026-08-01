using System.IO;
using System.Text.Json;
using Moq;
using YLproxy.GUI.ViewModels;
using YLproxy.Infrastructure;
using YLproxy.Models;
using Xunit;

namespace YLproxy.Tests.ViewModels;

public class ImportExportViewModelTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly FakeFileDialogService _dialog;
    private readonly FakeNotificationService _notifications;
    private readonly ImportExportViewModel _viewModel;

    public ImportExportViewModelTests()
    {
        _mockLogger = new Mock<ILogger>();
        _dialog = new FakeFileDialogService();
        _notifications = new FakeNotificationService();
        _viewModel = new ImportExportViewModel(_mockLogger.Object, _dialog, _notifications);
    }

    [Fact]
    public void ExportToJson_EmptyList_ShowsInfoAndReturnsEarly()
    {
        // Arrange
        var proxies = new List<ProxyItem>();

        // Act
        _viewModel.ExportToJson(proxies);

        // Assert
        Assert.Contains(_notifications.InfoMessages, m => m.Contains("没有可导出的代理"));
        Assert.False(_viewModel.IsExporting);
    }

    [Fact]
    public void ExportToJson_ValidList_CreatesJsonFile()
    {
        // Arrange
        var proxies = new List<ProxyItem>
        {
            new ProxyItem
            {
                Id = 1,
                Name = "Test Proxy",
                RemoteHost = "1.1.1.1",
                RemotePort = 8080,
                Username = "user1",
                Password = "pass1",
                Group = "Group1",
                LocalHost = "127.0.0.1",
                LocalPort = 9001,
                Status = ProxyStatus.Stopped,
                CreateTime = DateTime.UtcNow
            }
        };
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}.json");
        _dialog.SavePath = tempFile;

        try
        {
            // Act
            _viewModel.ExportToJson(proxies);

            // Assert
            Assert.True(File.Exists(tempFile));
            var json = File.ReadAllText(tempFile);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("Proxies", out var proxiesElement));
            Assert.Equal(1, proxiesElement.GetArrayLength());
            Assert.False(_viewModel.IsExporting);
            Assert.Contains(_notifications.InfoMessages, m => m.Contains("成功导出"));
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ExportToJson_WhenDialogCanceled_DoesNotWriteFile()
    {
        // Arrange
        var proxies = new List<ProxyItem>
        {
            new() { Id = 1, Name = "Test", RemoteHost = "1.1.1.1", RemotePort = 8080, LocalHost = "127.0.0.1", LocalPort = 9001 }
        };
        _dialog.SavePath = null;

        // Act
        _viewModel.ExportToJson(proxies);

        // Assert
        Assert.False(_viewModel.IsExporting);
        Assert.DoesNotContain(_notifications.InfoMessages, m => m.Contains("成功导出"));
    }

    [Fact]
    public void ImportFromJson_ValidJson_AddsProxiesToCollection()
    {
        // Arrange
        var proxies = new List<ProxyItem>();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_import_{System.Guid.NewGuid()}.json");

        // Create test JSON
        var exportData = new
        {
            Proxies = new[]
            {
                new
                {
                    Name = "Imported Proxy 1",
                    RemoteHost = "2.2.2.2",
                    RemotePort = 8081,
                    Username = "",
                    Password = "",
                    Group = "ImportedGroup",
                    LocalHost = "127.0.0.1",
                    LocalPort = 0, // Will be assigned
                    Status = (int)ProxyStatus.Stopped,
                    CreateTime = DateTime.UtcNow.ToString("O")
                },
                new
                {
                    Name = "Imported Proxy 2",
                    RemoteHost = "3.3.3.3",
                    RemotePort = 8082,
                    Username = "",
                    Password = "",
                    Group = "ImportedGroup",
                    LocalHost = "127.0.0.1",
                    LocalPort = 0, // Will be assigned
                    Status = (int)ProxyStatus.Stopped,
                    CreateTime = DateTime.UtcNow.ToString("O")
                }
            }
        };

        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(tempFile, json);
        _dialog.OpenPath = tempFile;

        try
        {
            // Act
            _viewModel.ImportFromJson(proxies);

            // Assert
            Assert.Equal(2, proxies.Count);
            Assert.Equal("Imported Proxy 1", proxies[0].Name);
            Assert.Equal("2.2.2.2", proxies[0].RemoteHost);
            Assert.Equal(8081, proxies[0].RemotePort);
            Assert.Equal("Imported Proxy 2", proxies[1].Name);
            Assert.Equal("3.3.3.3", proxies[1].RemoteHost);
            Assert.Equal(8082, proxies[1].RemotePort);
            Assert.False(_viewModel.IsImporting);
            Assert.Contains(_notifications.InfoMessages, m => m.Contains("成功导入"));
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ImportFromJson_InvalidJson_MissingProxiesArray_ShowsErrorAndReturnsEarly()
    {
        // Arrange
        var proxies = new List<ProxyItem>();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_import_invalid_{System.Guid.NewGuid()}.json");

        // Create invalid JSON (missing Proxies array)
        var invalidData = new { Message = "Invalid data" };
        var json = JsonSerializer.Serialize(invalidData);
        File.WriteAllText(tempFile, json);
        _dialog.OpenPath = tempFile;

        try
        {
            // Act
            _viewModel.ImportFromJson(proxies);

            // Assert
            Assert.Empty(proxies);
            Assert.Contains(_notifications.ErrorMessages, m => m.Contains("缺少 'Proxies' 数组"));
            Assert.False(_viewModel.IsImporting);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ImportFromJson_ValidJsonWithInvalidEntries_SkipsInvalidEntries()
    {
        // Arrange
        var proxies = new List<ProxyItem>();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_import_mixed_{System.Guid.NewGuid()}.json");

        // Create JSON with one valid and one invalid entry
        var exportData = new
        {
            Proxies = new object[]
            {
                new
                {
                    Name = "Valid Proxy",
                    RemoteHost = "4.4.4.4",
                    RemotePort = 8083,
                    Username = "",
                    Password = "",
                    Group = "ValidGroup",
                    LocalHost = "127.0.0.1",
                    LocalPort = 0,
                    Status = (int)ProxyStatus.Stopped,
                    CreateTime = DateTime.UtcNow.ToString("O")
                },
                new { Invalid = "Entry" } // Missing required fields
            }
        };

        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(tempFile, json);
        _dialog.OpenPath = tempFile;

        try
        {
            // Act
            _viewModel.ImportFromJson(proxies);

            // Assert
            Assert.Single(proxies);
            Assert.Equal("Valid Proxy", proxies[0].Name);
            Assert.Equal("4.4.4.4", proxies[0].RemoteHost);
            Assert.Equal(8083, proxies[0].RemotePort);
            Assert.False(_viewModel.IsImporting);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private sealed class FakeFileDialogService : IFileDialogService
    {
        public string? SavePath { get; set; }
        public string? OpenPath { get; set; }

        public string? ShowSaveJsonPath(string? initialDirectory) => SavePath;
        public string? ShowOpenJsonPath(string? initialDirectory) => OpenPath;
    }

    private sealed class FakeNotificationService : IUserNotificationService
    {
        public List<string> InfoMessages { get; } = new();
        public List<string> ErrorMessages { get; } = new();

        public void ShowInfo(string message, string title)
        {
            InfoMessages.Add($"{title}:{message}");
        }

        public void ShowError(string message, string title)
        {
            ErrorMessages.Add($"{title}:{message}");
        }
    }
}
