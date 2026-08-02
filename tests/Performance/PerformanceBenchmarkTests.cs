using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using YLproxy.Infrastructure.Services;
using YLproxy.Models;

namespace YLproxy.Tests.Performance;

/// <summary>
/// 性能基准测试：UI 响应、内存使用、并发压力。
/// </summary>
[Trait("TestCategory", "Performance")]
public class PerformanceBenchmarkTests
{
    [Fact]
    public void ProxyDataService_LoadLargeConfig_CompletesWithin500ms()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ylproxy_perf_{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(tempDir);
        var configPath = System.IO.Path.Combine(tempDir, "config.json");

        try
        {
            var service = new YLproxy.Core.ProxyDataService(configPath);
            var config = new YLproxy.Models.AppConfig
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
}
