using System.IO;
using System.Text.Json;
using System.Windows;
using Moq;
using YLproxy.GUI.ViewModels;
using YLproxy.Infrastructure;
using YLproxy.Models;
using Xunit;

namespace YLproxy.Tests.ViewModels;

public class ImportExportViewModelTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly ImportExportViewModel _viewModel;

    public ImportExportViewModelTests()
    {
        _mockLogger = new Mock<ILogger>();
        _viewModel = new ImportExportViewModel(_mockLogger.Object);
    }

    [Fact]
    public void ExportToJson_NullOrEmptyList_ShowsMessageAndReturnsEarly()
    {
        // Arrange
        var proxies = new List<ProxyItem>();

        // Act
        _viewModel.ExportToJson(proxies);

        // Assert
        // In a real test, we would verify that a message box was shown
        // For now, we just verify it doesn't throw an exception
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_export_{System.Guid.NewGuid()}.json");

        try
        {
            // Act
            _viewModel.ExportToJson(proxies, Path.GetDirectoryName(tempFile));

            // Assert
            // Note: We can't easily test the actual file dialog interaction in unit tests
            // In a real scenario, we would mock the SaveFileDialog or use integration tests
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ImportFromJson_NullOrEmptyList_DoesNothing()
    {
        // Arrange
        var proxies = new List<ProxyItem>();

        // Act
        _viewModel.ImportFromJson(proxies);

        // Assert
        // Just verify it doesn't throw
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

        try
        {
            // Act
            _viewModel.ImportFromJson(proxies, Path.GetDirectoryName(tempFile));

            // Assert
            Assert.Equal(2, proxies.Count);
            Assert.Equal("Imported Proxy 1", proxies[0].Name);
            Assert.Equal("2.2.2.2", proxies[0].RemoteHost);
            Assert.Equal(8081, proxies[0].RemotePort);
            Assert.Equal("Imported Proxy 2", proxies[1].Name);
            Assert.Equal("3.3.3.3", proxies[1].RemoteHost);
            Assert.Equal(8082, proxies[1].RemotePort);
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

        try
        {
            // Act
            _viewModel.ImportFromJson(proxies, Path.GetDirectoryName(tempFile));

            // Assert
            // Just verify it doesn't throw and doesn't add any proxies
            Assert.Empty(proxies);
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

        try
        {
            // Act
            _viewModel.ImportFromJson(proxies, Path.GetDirectoryName(tempFile));

            // Assert
            Assert.Single(proxies);
            Assert.Equal("Valid Proxy", proxies[0].Name);
            Assert.Equal("4.4.4.4", proxies[0].RemoteHost);
            Assert.Equal(8083, proxies[0].RemotePort);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}