using System;
using System.Reflection;
using YLproxy.Infrastructure.Services;
using Xunit;

namespace YLproxy.Tests.Infrastructure;

public sealed class TrafficMonitorServiceTests
{
    [Fact]
    public void RecordTraffic_ShouldAccumulateStats()
    {
        using var service = new TrafficMonitorService();

        service.RecordTraffic(1, 100, 200);
        service.RecordTraffic(1, 50, 60);

        var stats = service.GetStats(1);
        Assert.Equal(150, stats.BytesSent);
        Assert.Equal(260, stats.BytesReceived);
    }

    [Fact]
    public void RemoveStats_ShouldDeleteEntry()
    {
        using var service = new TrafficMonitorService();

        service.RecordTraffic(2, 10, 20);
        service.RemoveStats(2);

        var stats = service.GetStats(2);
        Assert.Equal(0, stats.BytesSent);
        Assert.Equal(0, stats.BytesReceived);
    }

    [Fact]
    public void GetAllStats_ShouldContainRecordedItems()
    {
        using var service = new TrafficMonitorService();

        service.RecordTraffic(11, 1, 1);
        service.RecordTraffic(12, 1, 1);

        var all = service.GetAllStats();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void OnTimerTick_ShouldRaiseOnStatsUpdated()
    {
        using var service = new TrafficMonitorService();
        var raised = false;
        service.OnStatsUpdated += () => raised = true;

        var method = typeof(TrafficMonitorService).GetMethod("OnTimerTick", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        method!.Invoke(service, new object?[] { null });

        Assert.True(raised);
    }
}
