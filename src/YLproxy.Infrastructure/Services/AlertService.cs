using YLproxy.Infrastructure.Abstractions;

namespace YLproxy.Infrastructure.Services;

public sealed class AlertService : IAlertService
{
    private readonly List<AlertRecord> _records = [];
    private readonly object _gate = new();
    private readonly int _maxRecords;

    public AlertService(int maxRecords = 1000)
    {
        _maxRecords = Math.Max(100, maxRecords);
    }

    public event Action<AlertRecord>? AlertRaised;

    public AlertRecord Raise(string level, string title, string message, string? source = null)
    {
        var record = new AlertRecord
        {
            Level = string.IsNullOrWhiteSpace(level) ? "Info" : level.Trim(),
            Title = title?.Trim() ?? string.Empty,
            Message = message?.Trim() ?? string.Empty,
            Source = source?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
        };

        lock (_gate)
        {
            _records.Add(record);
            if (_records.Count > _maxRecords)
                _records.RemoveRange(0, _records.Count - _maxRecords);
        }

        AlertRaised?.Invoke(record);
        return record;
    }

    public IReadOnlyList<AlertRecord> GetRecent(int take = 100)
    {
        var size = Math.Max(1, take);
        lock (_gate)
        {
            return _records
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(size)
                .ToList();
        }
    }
}
