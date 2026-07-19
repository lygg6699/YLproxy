using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YLproxy.Core.Config;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Utils;
using Xunit;

namespace YLproxy.Tests;

public sealed class ProxyDataServiceTests
{
    private static string GetTempConfigPath() =>
        Path.Combine(Path.GetTempPath(), $"ylproxy_test_{Guid.NewGuid():N}.json");

    [Fact]
    public void Save_ShouldCreateFile()
    {
        var path = GetTempConfigPath();
        try
        {
            var svc = new ProxyDataService(path, skipPathValidation: true);
            var cfg = new AppConfig();
            cfg.Proxies.Add(new ProxyItem { Id = 1, Name = "test", RemoteHost = "127.0.0.1", RemotePort = 8080 });
            svc.Save(cfg);

            Assert.True(File.Exists(path));
            var loaded = svc.Load();
            Assert.Single(loaded.Proxies);
            Assert.Equal(1, loaded.Proxies[0].Id);
        }
        finally
        {
            try { File.Delete(path); } catch (Exception ex)
            {
                // Ignore cleanup errors during test operations
            }
        }
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ShouldReturnEmptyAndCreateFile()
    {
        var path = GetTempConfigPath();
        try
        {
            var svc = new ProxyDataService(path, skipPathValidation: true);
            var cfg = svc.Load();
            Assert.NotNull(cfg);
            Assert.NotNull(cfg.Proxies);
            Assert.Empty(cfg.Proxies);
            Assert.True(File.Exists(path));
        }
        finally
        {
            try { File.Delete(path); } catch (Exception ex)
            {
                // Ignore cleanup errors during test operations
            }
        }
    }

    [Fact]
    public async Task LoadAsync_ShouldWork()
    {
        var path = GetTempConfigPath();
        try
        {
            var svc = new ProxyDataService(path, skipPathValidation: true);
            var cfg = new AppConfig();
            cfg.Proxies.Add(new ProxyItem { Id = 1, Name = "test", RemoteHost = "127.0.0.1", RemotePort = 8080 });
            svc.Save(cfg);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var loaded = await svc.LoadAsync(cts.Token);
            Assert.Single(loaded.Proxies);
            Assert.Equal(1, loaded.Proxies[0].Id);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public async Task SaveAsync_ShouldWork()
    {
        var path = GetTempConfigPath();
        try
        {
            var svc = new ProxyDataService(path, skipPathValidation: true);
            var cfg = new AppConfig();
            cfg.Proxies.Add(new ProxyItem { Id = 2, Name = "async_test", RemoteHost = "10.0.0.1", RemotePort = 3128 });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await svc.SaveAsync(cfg, cts.Token);

            Assert.True(File.Exists(path));
            var loaded = svc.Load();
            Assert.Single(loaded.Proxies);
            Assert.Equal(2, loaded.Proxies[0].Id);
        }
        finally
        {
            try { File.Delete(path); } catch (Exception ex)
            {
                // Ignore cleanup errors during test operations
            }
        }
    }

    [Fact]
    public void Constructor_WithNullPath_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new ProxyDataService(null!, skipPathValidation: true));
    }

    [Fact]
    public void Constructor_WithEmptyPath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new ProxyDataService("", skipPathValidation: true));
    }

    [Fact]
    public void Constructor_RelativePath_ShouldResolve()
    {
        // skipPathValidation allows non-canonical path
        var svc = new ProxyDataService("test_data.json", skipPathValidation: true);
        Assert.NotNull(svc.ConfigPath);
        Assert.EndsWith("test_data.json", svc.ConfigPath);
    }

    [Fact]
    public void CredentialsResetDuringLoad_DefaultsToFalse()
    {
        var path = GetTempConfigPath();
        try
        {
            var svc = new ProxyDataService(path, skipPathValidation: true);
            Assert.False(svc.CredentialsResetDuringLoad);
        }
        finally
        {
            try { File.Delete(path); } catch (Exception ex)
            {
                // Ignore cleanup errors during test operations
            }
        }
    }

    [Fact]
    public void Save_NullConfig_ShouldThrow()
    {
        var path = GetTempConfigPath();
        try
        {
            var svc = new ProxyDataService(path, skipPathValidation: true);
            Assert.Throws<ArgumentNullException>(() => svc.Save(null!));
        }
        finally
        {
            try { File.Delete(path); } catch (Exception ex)
            {
                // Ignore cleanup errors during test operations
            }
        }
    }
}
