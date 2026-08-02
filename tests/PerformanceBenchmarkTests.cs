using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using YLproxy.Core;
using YLproxy.Infrastructure;
using YLproxy.Infrastructure.Abstractions;
using YLproxy.Infrastructure.Services;
using YLproxy.Models;

namespace YLproxy.Tests;

/// <summary>
/// 性能基准测试：UI 响应、内存使用、并发压力。
/// 这些测试验证关键路径在合理时间内完成，避免性能回归。
/// </summary>
[Trait("TestCategory", "Performance")]
public class PerformanceBenchmarkTests
{
    /// <summary>
    /// UI 响应性能：ProxyDataService 加载 1000 代理配置应在 500ms 内完成。
    /// </summary>
    [Fact]
    public void ProxyDataService_LoadLargeConfig_CompletesWithin500ms()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ylproxy_perf_{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(tempDir);
        var configPath = System.IO.Path.Combine(tempDir, "config.json");

        try
        {
            var service = new ProxyDataService(configPath);
            var config = new AppConfig
            {
                Version = "1.1",
                Proxies = Enumerable.Range(1, 1000)
                    .Select(i => new ProxyItem
                    {
                        Id = i,
                        Name = $"Proxy-{i}",
                        RemoteHost = $"host{i}.example.com",
                        RemotePort = 8080,
                        LocalHost = "127.0.0.1",
                        LocalPort = 9000 + i,
                        Status = ProxyStatus.Stopped
                    }).ToList()
            };
            service.Save(config);

            var sw = Stopwatch.StartNew();
            var loaded = service.Load();
            sw.Stop();

            Assert.Equal(1000, loaded.Proxies.Count);
            Assert.True(sw.ElapsedMilliseconds < 500, $"Load took {sw.ElapsedMilliseconds}ms, expected < 500ms");
        }
        finally
        {
            try { System.IO.Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// 内存使用：加载 1000 代理配置后内存增长应合理（小于 10MB）。
    /// </summary>
    [Fact]
    public void ProxyDataService_LargeConfig_MemoryUsageReasonable()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ylproxy_mem_{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(tempDir);
        var configPath = System.IO.Path.Combine(tempDir, "config.json");

        try
        {
            var service = new ProxyDataService(configPath);
            var config = new AppConfig
            {
                Version = "1.1",
                Proxies = Enumerable.Range(1, 1000)
                    .Select(i => new ProxyItem
                    {
                        Id = i,
                        Name = $"Proxy-{i}",
                        RemoteHost = $"host{i}.example.com",
                        RemotePort = 8080,
                        LocalPort = 9000 + i
                    }).ToList()
            };
            service.Save(config);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            var beforeBytes = GC.GetTotalMemory(forceFullCollection: true);

            var loaded = service.Load();

            var afterBytes = GC.GetTotalMemory(forceFullCollection: false);
            var deltaBytes = afterBytes - beforeBytes;

            Assert.Equal(1000, loaded.Proxies.Count);
            Assert.True(deltaBytes < 10 * 1024 * 1024, $"Memory delta {deltaBytes / 1024}KB, expected < 10MB");
        }
        finally
        {
            try { System.IO.Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// 并发压力：TrafficMonitorService 在 50 并发 RecordTraffic 调用下应线程安全且快速完成。
    /// </summary>
    [Fact]
    public void TrafficMonitorService_ConcurrentRecord_CompletesWithin1s()
    {
        using var monitor = new TrafficMonitorService();
        var sw = Stopwatch.StartNew();

        Parallel.For(0, 50, proxyId =>
        {
            for (int i = 0; i < 100; i++)
            {
                monitor.RecordTraffic(proxyId, bytesSent: 1024, bytesReceived: 2048);
            }
        });

        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000, $"Concurrent record took {sw.ElapsedMilliseconds}ms, expected < 1000ms");

        for (int i = 0; i < 50; i++)
        {
            var stats = monitor.GetStats(i);
            Assert.Equal(100 * 1024L, stats.BytesSent);
            Assert.Equal(100 * 2048L, stats.BytesReceived);
        }
    }

    /// <summary>
    /// 并发压力：ProxyDataService 并发 Save+Load 不应导致数据损坏（文件锁保护）。
    /// </summary>
    [Fact]
    public async Task ProxyDataService_ConcurrentSaveLoad_NoCorruption()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ylproxy_conc_{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(tempDir);
        var configPath = System.IO.Path.Combine(tempDir, "config.json");

        try
        {
            var service = new ProxyDataService(configPath);
            var initialConfig = new AppConfig
            {
                Version = "1.1",
                Proxies = new List<ProxyItem>
                {
                    new() { Id = 1, Name = "Base", RemoteHost = "1.1.1.1", RemotePort = 8080, LocalPort = 9000 }
                }
            };
            service.Save(initialConfig);

            var exceptions = new ConcurrentBag<Exception>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            var writers = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        var cfg = new AppConfig
                        {
                            Version = "1.1",
                            Proxies = new List<ProxyItem>
                            {
                                new() { Id = 1, Name = "Updated", RemoteHost = "2.2.2.2", RemotePort = 8080, LocalPort = 9000 }
                            }
                        };
                        service.Save(cfg);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { exceptions.Add(ex); }
            }, cts.Token)).ToArray();

            var readers = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        var loaded = service.Load();
                        Assert.NotNull(loaded.Proxies);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { exceptions.Add(ex); }
            }, cts.Token)).ToArray();

            // 等待所有任务完成（cts 到期后任务会被取消，WhenAll 可能抛出 TaskCanceledException）
            try
            {
                await Task.WhenAll(writers.Concat(readers));
            }
            catch (OperationCanceledException)
            {
                // 预期：cts 到期后任务被取消
            }

            Assert.Empty(exceptions);

            var final = service.Load();
            Assert.Single(final.Proxies);
            Assert.Equal("1.1", final.Version);
        }
        finally
        {
            try { System.IO.Directory.Delete(tempDir, true); } catch { }
        }
    }
}
