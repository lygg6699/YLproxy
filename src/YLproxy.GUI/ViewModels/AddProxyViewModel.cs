using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using YLproxy.Core.Config;
using YLproxy.Models;

namespace YLproxy.GUI.ViewModels;

public sealed class AddProxyViewModel : ViewModelBase
{
    private readonly IList<ProxyItem> _existingProxies;
    private readonly string _configPath;
    private readonly int _portRangeStart;
    private readonly int _portRangeEnd;
    private readonly ProxyItem? _editTarget;
    private int _currentId;
    private bool _isEditMode;

    private string _title = "添加代理";
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _remoteHost = string.Empty;
    public string RemoteHost
    {
        get => _remoteHost;
        set => SetProperty(ref _remoteHost, value);
    }

    private string _remotePortText = string.Empty;
    public string RemotePortText
    {
        get => _remotePortText;
        set => SetProperty(ref _remotePortText, value);
    }

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private string _group = string.Empty;
    public string Group
    {
        get => _group;
        set => SetProperty(ref _group, value);
    }

    private bool _isAutoPort = true;
    public bool IsAutoPort
    {
        get => _isAutoPort;
        set => SetProperty(ref _isAutoPort, value);
    }

    private string _localPortText = string.Empty;
    public string LocalPortText
    {
        get => _localPortText;
        set => SetProperty(ref _localPortText, value);
    }

    private string _validationMessage = string.Empty;
    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    private bool _isPasswordVisible;
    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);
    }

    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }
    public ICommand TogglePasswordCommand { get; }

    public Action? CloseAction { get; set; }

    public AddProxyViewModel(
        IList<ProxyItem> existingProxies,
        string configPath,
        int portRangeStart = 9001,
        int portRangeEnd = 9100,
        ProxyItem? editTarget = null)
    {
        if (portRangeStart < 1 || portRangeStart > 65535)
            throw new ArgumentOutOfRangeException(nameof(portRangeStart));
        if (portRangeEnd < portRangeStart || portRangeEnd > 65535)
            throw new ArgumentOutOfRangeException(nameof(portRangeEnd));

        _existingProxies = existingProxies;
        _configPath = configPath;
        _portRangeStart = portRangeStart;
        _portRangeEnd = portRangeEnd;
        _editTarget = editTarget;
        _isEditMode = editTarget is not null;

        Title = _isEditMode ? "编辑代理" : "添加代理";

        if (_isEditMode && editTarget is not null)
        {
            _currentId = editTarget.Id;
            _name = editTarget.Name;
            _remoteHost = editTarget.RemoteHost;
            _remotePortText = editTarget.RemotePort.ToString();
            _username = editTarget.Username ?? "";
            _password = editTarget.Password ?? "";
            _group = editTarget.Group ?? "";
            _isAutoPort = false;
            _localPortText = editTarget.LocalPort.ToString();
        }
        else
        {
            _currentId = existingProxies.Count == 0 ? 1 : existingProxies.Max(p => p.Id) + 1;
            _name = $"Proxy-{_currentId}";
            _remoteHost = "";
            _remotePortText = "";
            _username = "";
            _password = "";
            _isAutoPort = true;
            _localPortText = "";
        }

        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
        TogglePasswordCommand = new RelayCommand(() => IsPasswordVisible = !IsPasswordVisible);
    }

    private void Confirm()
    {
        ValidationMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationMessage = "代理名称不能为空";
            return;
        }

        if (!TryParsePort(RemotePortText, out var remotePort))
        {
            ValidationMessage = "服务器端口必须是 1~65535 的整数";
            return;
        }

        if (string.IsNullOrWhiteSpace(RemoteHost) || !System.Net.IPAddress.TryParse(RemoteHost.Trim(), out _))
        {
            ValidationMessage = "服务器IP格式不正确";
            return;
        }

        int? requestedLocalPort = null;
        if (!IsAutoPort)
        {
            if (!TryParsePort(LocalPortText, out var lp))
            {
                ValidationMessage = "本地端口必须是 1~65535 的整数";
                return;
            }

            requestedLocalPort = lp;
        }

        var usedPorts = new HashSet<int>(_existingProxies
            .Where(p => _isEditMode || p.Id != _currentId)
            .Select(p => p.LocalPort));
        int localPort;

        if (IsAutoPort)
        {
            localPort = _portRangeStart;
            while (usedPorts.Contains(localPort))
            {
                localPort++;
                if (localPort > _portRangeEnd)
                {
                    ValidationMessage = $"端口已耗尽（{_portRangeStart}~{_portRangeEnd} 已全部占用）";
                    return;
                }
            }
        }
        else
        {
            localPort = requestedLocalPort!.Value;
            if (usedPorts.Contains(localPort))
            {
                ValidationMessage = "本地端口已被占用";
                return;
            }
        }

        var localIp = YLproxy.Utils.NetworkUtil.GetBestLocalIp() ?? string.Empty;

        try
        {
            var svc = new ProxyDataService(_configPath);
            var cfg = svc.Load();

            if (_isEditMode)
            {
                var existing = cfg.Proxies.FirstOrDefault(p => p.Id == _currentId);
                if (existing is not null)
                {
                    existing.Name = Name.Trim();
                    existing.RemoteHost = RemoteHost.Trim();
                    existing.RemotePort = remotePort;
                    existing.Username = Username ?? string.Empty;
                    existing.Password = Password ?? string.Empty;
                    existing.Group = (Group ?? string.Empty).Trim();
                    existing.LocalHost = localIp;
                    existing.LocalPort = localPort;
                }
            }
            else
            {
                var item = new ProxyItem
                {
                    Id = _currentId,
                    Name = Name.Trim(),
                    RemoteHost = RemoteHost.Trim(),
                    RemotePort = remotePort,
                    Username = Username ?? string.Empty,
                    Password = Password ?? string.Empty,
                    Group = (Group ?? string.Empty).Trim(),
                    LocalHost = localIp,
                    LocalPort = localPort,
                    Status = ProxyStatus.Stopped,
                    CreateTime = DateTime.UtcNow
                };

                if (!cfg.Proxies.Any(p => p.Id == item.Id || p.LocalPort == item.LocalPort))
                    cfg.Proxies.Add(item);
            }

            svc.Save(cfg);
            CloseAction?.Invoke();
        }
        catch (Exception ex)
        {
            ValidationMessage = $"保存失败：{ex.Message}";
        }
    }

    private static bool TryParsePort(string text, out int port)
    {
        port = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        if (!int.TryParse(text, out port)) return false;
        return port >= 1 && port <= 65535;
    }
}
