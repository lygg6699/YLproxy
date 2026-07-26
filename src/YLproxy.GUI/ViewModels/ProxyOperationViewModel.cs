using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YLproxy.Core.Abstractions;
using YLproxy.GUI.ViewModels;
using YLproxy.Infrastructure;
using YLproxy.Models;
using ProxyProcessManager = YLproxy.Proxy.Abstractions.IProxyProcessManager;
using CoreProxyTester = YLproxy.Core.Abstractions.IProxyTester;

namespace YLproxy.GUI.ViewModels;

/// <summary>
/// 负责代理操作，包括启动、停止、测试等
/// </summary>
public sealed class ProxyOperationViewModel : ViewModelBase
{
    private readonly CoreProxyTester _proxyTester;
    private readonly ProxyProcessManager _proxyProcessManager;
    private readonly ILogger _logger;
    private bool _isTesting;
    private bool _isStarting;
    private bool _isStopping;

    public bool IsTesting
    {
        get => _isTesting;
        set => SetProperty(ref _isTesting, value);
    }

    public bool IsStarting
    {
        get => _isStarting;
        set => SetProperty(ref _isStarting, value);
    }

    public bool IsStopping
    {
        get => _isStopping;
        set => SetProperty(ref _isStopping, value);
    }

    public ProxyOperationViewModel(
        CoreProxyTester proxyTester,
        ProxyProcessManager proxyProcessManager,
        ILogger logger)
    {
        _proxyTester = proxyTester;
        _proxyProcessManager = proxyProcessManager;
        _logger = logger;
    }

    /// <summary>
    /// 测试选中的代理
    /// </summary>
    public async Task TestSelectedProxyAsync(ProxyItem? proxy)
    {
        if (proxy == null) return;

        IsTesting = true;
        try
        {
            var (success, latency, error) = await _proxyTester.TestAsync(
                proxy.RemoteHost, proxy.RemotePort, proxy.Username, proxy.Password);
            
            proxy.Status = success ? ProxyStatus.Running : ProxyStatus.Failed;
            if (success)
            {
                _logger.Info($"Proxy test succeeded: {proxy.Name} ({proxy.RemoteHost}:{proxy.RemotePort}) - {latency}ms");
            }
            else
            {
                _logger.Warn($"Proxy test failed: {proxy.Name} - {error}");
            }
        }
        catch (Exception ex)
        {
            proxy.Status = ProxyStatus.Failed;
            _logger.Error($"Proxy test exception: {ex.Message}");
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>
    /// 启动选中的代理
    /// </summary>
    public void StartSelectedProxy(ProxyItem? proxy)
    {
        if (proxy == null) return;

        IsStarting = true;
        try
        {
            _proxyProcessManager.Start(proxy);
            proxy.Status = ProxyStatus.Running;
            _logger.Info($"Proxy started: {proxy.Name} ({proxy.RemoteHost}:{proxy.RemotePort})");
        }
        catch (Exception ex)
        {
            proxy.Status = ProxyStatus.Failed;
            _logger.Error($"Failed to start proxy: {ex.Message}");
        }
        finally
        {
            IsStarting = false;
        }
    }

    /// <summary>
    /// 停止选中的代理
    /// </summary>
    public void StopSelectedProxy(ProxyItem? proxy)
    {
        if (proxy == null) return;

        IsStopping = true;
        try
        {
            _proxyProcessManager.Stop(proxy);
            proxy.Status = ProxyStatus.Stopped;
            _logger.Info($"Proxy stopped: {proxy.Name} ({proxy.RemoteHost}:{proxy.RemotePort})");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to stop proxy: {ex.Message}");
        }
        finally
        {
            IsStopping = false;
        }
    }

    /// <summary>
    /// 批量启动选中的代理
    /// </summary>
    public void BatchStart(List<ProxyItem>? proxies)
    {
        if (proxies == null || proxies.Count == 0) return;

        foreach (var proxy in proxies)
        {
            StartSelectedProxy(proxy);
        }
    }

    /// <summary>
    /// 批量停止选中的代理
    /// </summary>
    public void BatchStop(List<ProxyItem>? proxies)
    {
        if (proxies == null || proxies.Count == 0) return;

        foreach (var proxy in proxies)
        {
            StopSelectedProxy(proxy);
        }
    }
}
