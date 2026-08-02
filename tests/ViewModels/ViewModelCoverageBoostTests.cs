using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using YLproxy.GUI;
using YLproxy.GUI.ViewModels;
using YLproxy.Infrastructure.Abstractions;
using YLproxy.Models;
using Xunit;

namespace YLproxy.Tests.ViewModels;

public sealed class ViewModelCoverageBoostTests
{
    [Fact]
    public void Dashboard_UpdateApiStatus_ShouldUpdateProperties()
    {
        var vm = new DashboardViewModel();
        vm.UpdateApiStatus("Running", 8899);

        Assert.Equal("Running", vm.ApiStatus);
        Assert.Equal(8899, vm.ApiPort);
    }

    [Fact]
    public void HostInfo_Setters_ShouldPersistValues()
    {
        var vm = new HostInfoViewModel
        {
            ComputerName = "MACHINE",
            IpAddress = "127.0.0.1",
            NetworkStatus = "Connected",
            Now = DateTime.Today
        };

        Assert.Equal("MACHINE", vm.ComputerName);
        Assert.Equal("127.0.0.1", vm.IpAddress);
        Assert.Equal("Connected", vm.NetworkStatus);
    }

    [Fact]
    public void GroupViewModel_AddRemoveRename_ShouldWork()
    {
        var vm = new GroupViewModel();

        vm.AddGroup("G1");
        vm.AddGroup("G2");
        vm.SelectedGroup = "G1";
        vm.RenameGroup("G1", "G1-new");
        vm.RemoveGroup("G2");

        Assert.Contains("全部", vm.Groups);
        Assert.Contains("G1-new", vm.Groups);
        Assert.DoesNotContain("G2", vm.Groups);
        Assert.Equal("G1-new", vm.SelectedGroup);
    }

    [Fact]
    public void GroupViewModel_LoadGroupsFromProxies_ShouldLoadDistinctGroups()
    {
        var vm = new GroupViewModel();
        var proxies = new ObservableCollection<ProxyItem>
        {
            new() { Group = "A" },
            new() { Group = "A" },
            new() { Group = "B" }
        };

        vm.LoadGroupsFromProxies(proxies);

        Assert.Equal(3, vm.Groups.Count);
        Assert.Equal("全部", vm.Groups[0]);
        Assert.Contains("A", vm.Groups);
        Assert.Contains("B", vm.Groups);
    }

    [Fact]
    public void LogPanel_AddFilterAndClear_ShouldWork()
    {
        var vm = new LogPanelViewModel();

        vm.AddRawLog("[INFO] hello");
        vm.AddRawLog("[ERROR] bad");
        vm.SelectedLogLevel = "Error";

        Assert.Single(vm.FilteredLogs);
        Assert.Equal(LogLevel.Error, vm.FilteredLogs[0].Level);

        vm.ClearLogCommand.Execute(null);
        Assert.Empty(vm.Logs);
        Assert.Empty(vm.FilteredLogs);
    }

    [Fact]
    public void ManageGroupsViewModel_AddDeleteCommands_ShouldInvokeCallbacks()
    {
        var addCount = 0;
        var deleteCount = 0;

        var vm = new ManageGroupsViewModel(
            new ObservableCollection<string> { "全部", "A" },
            onAddGroup: _ => addCount++,
            onDeleteGroup: _ => deleteCount++);

        vm.AddGroupCommand.Execute("B");
        vm.SelectedGroup = "A";
        vm.DeleteGroupCommand.Execute(null);

        Assert.Contains("B", vm.Groups);
        Assert.DoesNotContain("A", vm.Groups);
        Assert.Equal(1, addCount);
        Assert.Equal(1, deleteCount);
    }

    [Fact]
    public void TrafficStatsViewModel_RefreshAndClear_ShouldWork()
    {
        using var monitor = new FakeTrafficMonitorService();
        monitor.RecordTraffic(1, 100, 200);
        var vm = new TrafficStatsViewModel(monitor);

        vm.RefreshStats(new[]
        {
            new ProxyItem { Id = 1, Name = "p1" }
        });

        Assert.Single(vm.Stats);
        Assert.Equal(100, vm.Stats[0].BytesSent);

        vm.Clear();
        Assert.Empty(vm.Stats);
    }

    [Fact]
    public void AddProxyViewModel_Confirm_AutoPort_ShouldPersistToConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ylproxy_addvm_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");

        try
        {
            var vm = new AddProxyViewModel(
                existingProxies: Array.Empty<ProxyItem>(),
                configPath: configPath,
                portRangeStart: 9001,
                portRangeEnd: 9002);

            var closed = false;
            vm.CloseAction = () => closed = true;
            vm.Name = "T1";
            vm.RemoteHost = "1.2.3.4";
            vm.RemotePortText = "8080";
            vm.IsAutoPort = true;

            vm.ConfirmCommand.Execute(null);

            Assert.True(closed);
            var json = File.ReadAllText(configPath);
            Assert.Contains("\"Name\": \"T1\"", json);
            Assert.Contains("\"LocalPort\": 9001", json);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AddProxyViewModel_Confirm_WhenPortUsed_ShouldSetValidationMessage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ylproxy_addvm_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");

        try
        {
            var existing = new[]
            {
                new ProxyItem { Id = 1, LocalPort = 9001 },
                new ProxyItem { Id = 2, LocalPort = 9002 }
            };

            var vm = new AddProxyViewModel(existing, configPath, 9001, 9002)
            {
                Name = "T2",
                RemoteHost = "1.2.3.4",
                RemotePortText = "8080",
                IsAutoPort = true
            };

            vm.ConfirmCommand.Execute(null);

            Assert.Contains("端口已耗尽", vm.ValidationMessage);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    private sealed class FakeTrafficMonitorService : ITrafficMonitorService
    {
        private readonly Dictionary<int, TrafficStats> _stats = new();

        public event Action? OnStatsUpdated;

        public void RecordTraffic(int proxyId, long bytesSent, long bytesReceived)
        {
            if (!_stats.TryGetValue(proxyId, out var item))
            {
                item = new TrafficStats();
                _stats[proxyId] = item;
            }

            item.BytesSent += bytesSent;
            item.BytesReceived += bytesReceived;
            item.LastUpdate = DateTime.UtcNow;
            OnStatsUpdated?.Invoke();
        }

        public TrafficStats GetStats(int proxyId)
        {
            return _stats.TryGetValue(proxyId, out var stats)
                ? stats
                : new TrafficStats();
        }

        public void RemoveStats(int proxyId)
        {
            _stats.Remove(proxyId);
        }

        public void Dispose()
        {
            _stats.Clear();
        }
    }
}
