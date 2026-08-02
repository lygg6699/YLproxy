namespace YLproxy.Infrastructure.Abstractions;

public interface IAlertService
{
    event Action<AlertRecord>? AlertRaised;

    AlertRecord Raise(string level, string title, string message, string? source = null);

    IReadOnlyList<AlertRecord> GetRecent(int take = 100);
}

public sealed class AlertRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Level { get; set; } = "Info";

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Source { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
