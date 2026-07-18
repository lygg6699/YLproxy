using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using YLproxy.GUI;

namespace YLproxy.GUI.ViewModels;

public sealed class LogPanelViewModel : ViewModelBase
{
    private readonly ObservableCollection<LogEntry> _logs = new();
    public ObservableCollection<LogEntry> Logs => _logs;

    private readonly ObservableCollection<LogEntry> _filteredLogs = new();
    public ObservableCollection<LogEntry> FilteredLogs => _filteredLogs;

    public List<string> LogLevels { get; } = new() { "全部", "Info", "Warn", "Error" };

    private string _selectedLogLevel = "全部";
    public string SelectedLogLevel
    {
        get => _selectedLogLevel;
        set
        {
            SetProperty(ref _selectedLogLevel, value);
            ApplyLogFilter();
        }
    }

    private string _statusMessage = string.Empty;

    public RelayCommand ClearLogCommand { get; }

    public LogPanelViewModel()
    {
        ClearLogCommand = new RelayCommand(() =>
        {
            _logs.Clear();
            _filteredLogs.Clear();
        });
    }

    public void AddRawLog(string rawMessage)
    {
        var entry = LogEntry.FromRawString(rawMessage);
        _logs.Add(entry);
        _filteredLogs.Add(entry);
    }

    public void AppendEntry(LogEntry entry)
    {
        _logs.Add(entry);
        if (IsLogVisible(entry))
            _filteredLogs.Add(entry);
    }

    public void ApplyLogFilter()
    {
        _filteredLogs.Clear();
        foreach (var l in _logs)
        {
            if (IsLogVisible(l))
                _filteredLogs.Add(l);
        }
    }

    private bool IsLogVisible(LogEntry l)
    {
        return _selectedLogLevel switch
        {
            "Info" => l.Level != LogLevel.Debug,
            "Warn" => l.Level is LogLevel.Warn || l.Level is LogLevel.Error || l.Level is LogLevel.Fatal,
            "Error" => l.Level is LogLevel.Error || l.Level is LogLevel.Fatal,
            _ => true,
        };
    }

}

