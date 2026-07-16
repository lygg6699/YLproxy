namespace YLproxy.Api;

public sealed class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponse<T> Fail(string error) => new() { Success = false, Error = error };
}

public sealed class ProxyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RemoteHost { get; set; } = string.Empty;
    public int RemotePort { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string LocalHost { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public string Status { get; set; } = "Stopped";
    public DateTime CreateTime { get; set; }
}

public sealed class ProxyTestResult
{
    public bool Success { get; set; }
    public long LatencyMs { get; set; }
    public string? Error { get; set; }
}
