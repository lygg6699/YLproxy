using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using YLproxy.Infrastructure;
using YLproxy.Models;

namespace YLproxy.Proxy;

/// <summary>
/// .NET 托管的代理转发器——替代 3proxy parent http 认证不兼容问题。
/// 客户端连入本地端口后，使用 HttpClient + WebProxy 通过上游代理转发请求，
/// .NET 内置代理栈自动处理 407 挑战-响应、Basic/Digest/NTLM 等认证协商。
/// </summary>
public sealed class ManagedProxyForwarder : IDisposable
{
    private readonly TcpListener _listener;
    private readonly HttpClient _upstreamClient;
    private readonly string _upstreamHost;
    private readonly int _upstreamPort;
    private readonly string? _username;
    private readonly string? _password;
    private readonly ILogger _logger;
    private readonly string _proxyName;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _concurrencyLimiter;
    private int _disposed;

    private const int MaxConcurrentClients = 100;

    public ManagedProxyForwarder(ProxyItem proxy, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        _proxyName = proxy.Name ?? $"proxy-{proxy.Id}";
        _upstreamHost = proxy.RemoteHost;
        _upstreamPort = proxy.RemotePort;
        _username = proxy.Username;
        _password = proxy.Password;
        _logger = logger ?? LoggerFactory.CreateLogger();
        _concurrencyLimiter = new SemaphoreSlim(MaxConcurrentClients, MaxConcurrentClients);

        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy($"http://{proxy.RemoteHost}:{proxy.RemotePort}"),
            UseProxy = true,
            AllowAutoRedirect = false,
        };

        if (!string.IsNullOrEmpty(proxy.Username) && !string.IsNullOrEmpty(proxy.Password))
        {
            handler.Proxy!.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
            // 预认证：避免 407 往返，直接发送 Basic auth
            handler.PreAuthenticate = true;
            handler.DefaultProxyCredentials = handler.Proxy.Credentials;
        }

        _upstreamClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        // 为 CONNECT 隧道设置 Authorization 头，使其对 DefaultRequestHeaders 可见。
        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            var authBytes = System.Text.Encoding.UTF8.GetBytes($"{_username}:{_password}");
            var basicValue = System.Convert.ToBase64String(authBytes);
            _upstreamClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicValue);
        }

        _listener = new TcpListener(IPAddress.Loopback, proxy.LocalPort);
        LocalPort = proxy.LocalPort;
    }

    public int LocalPort { get; }

    public void Start()
    {
        _listener.Start();
        _ = AcceptLoopAsync();
        _logger.Debug($"ManagedProxyForwarder [{_proxyName}] started on 127.0.0.1:{LocalPort}");
    }

    private async Task AcceptLoopAsync()
    {
        var token = _cts.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient? client = null;
                try { client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) when (token.IsCancellationRequested) { return; }
                catch { continue; }

                // Acquire semaphore before processing (P3-1 concurrency limit)
                await _concurrencyLimiter.WaitAsync(token).ConfigureAwait(false);

                _ = Task.Run(() => HandleClientAsync(client, token), token)
                    .ContinueWith(t =>
                    {
                        _concurrencyLimiter.Release();
                        if (t.IsFaulted)
                            _logger.Warn($"ManagedProxyForwarder [{_proxyName}] client fault: {t.Exception?.InnerException?.Message}");
                    }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (ObjectDisposedException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
                _logger.Warn($"ManagedProxyForwarder [{_proxyName}] accept loop: {ex.Message}");
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            try
            {
                var clientStream = client.GetStream();

                // 读取客户端原始 HTTP 请求
                var reqBuf = new byte[65536];
                var totalRead = 0;
                var headerEnd = -1;

                while (headerEnd < 0 && totalRead < reqBuf.Length)
                {
                    var r = await clientStream.ReadAsync(reqBuf.AsMemory(totalRead, reqBuf.Length - totalRead), token).ConfigureAwait(false);
                    if (r <= 0) break;
                    totalRead += r;

                    // 寻找 \r\n\r\n 头结束标记
                    for (var i = 0; i <= totalRead - 4; i++)
                    {
                        if (reqBuf[i] == '\r' && reqBuf[i + 1] == '\n' && reqBuf[i + 2] == '\r' && reqBuf[i + 3] == '\n')
                        {
                            headerEnd = i + 4;
                            break;
                        }
                    }

                    if (totalRead > 65536) break;
                }

                if (totalRead == 0) return;

                var headerText = headerEnd >= 0
                    ? Encoding.ASCII.GetString(reqBuf, 0, headerEnd)
                    : Encoding.ASCII.GetString(reqBuf, 0, totalRead);

                var firstLine = headerText.Split("\r\n")[0];
                var methodUrl = firstLine.Split(' ');

                if (methodUrl.Length < 2)
                {
                    await this.WriteError(clientStream, 400, "Bad Request");
                    return;
                }

                var method = methodUrl[0];
                var url = methodUrl[1];

                // 如果是 CONNECT 隧道（HTTPS）
                if (method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleConnectAsync(clientStream, url, token).ConfigureAwait(false);
                    return;
                }

                // 普通 HTTP 方法：用 HttpClient 通过上游代理请求
                using var request = new HttpRequestMessage(new HttpMethod(method), url);

                // 复制头部（跳过 Host 和 Proxy-Authorization）
                var lines = headerText.Split("\r\n");
                for (var i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var colon = line.IndexOf(':');
                    if (colon < 0) continue;
                    var name = line[..colon].Trim();
                    if (name.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Proxy-Connection", StringComparison.OrdinalIgnoreCase))
                        continue;
                    try { request.Headers.TryAddWithoutValidation(name, line[(colon + 1)..].Trim()); }
                    catch (Exception ex)
                    {
                        _logger.Debug($"Failed to add header '{name}': {ex.Message}");
                    }
                }

                // 如果有 body，写入
                if (headerEnd >= 0 && headerEnd < totalRead)
                {
                    var bodyLen = totalRead - headerEnd;
                    request.Content = new ByteArrayContent(reqBuf, headerEnd, bodyLen);
                    // 复制 Content-Type
                    var ct = headerText.Split("\r\n")
                        .FirstOrDefault(l => l.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase));
                    if (ct is not null)
                    {
                        var ctVal = ct[(ct.IndexOf(':') + 1)..].Trim();
                        request.Content.Headers.TryAddWithoutValidation("Content-Type", ctVal);
                    }
                }

                using var response = await _upstreamClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);

                // 将响应写回客户端
                var respSb = new StringBuilder();
                respSb.Append(CultureInfo.InvariantCulture, $"HTTP/1.1 {(int)response.StatusCode} {response.ReasonPhrase}\r\n");
                foreach (var h in response.Headers)
                    respSb.Append(CultureInfo.InvariantCulture, $"{h.Key}: {string.Join(", ", h.Value)}\r\n");
                foreach (var h in response.Content.Headers)
                    respSb.Append(CultureInfo.InvariantCulture, $"{h.Key}: {string.Join(", ", h.Value)}\r\n");
                respSb.Append("\r\n");

                var respBytes = Encoding.ASCII.GetBytes(respSb.ToString());
                await clientStream.WriteAsync(respBytes, token).ConfigureAwait(false);

                using var respStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                await respStream.CopyToAsync(clientStream, token).ConfigureAwait(false);
                await clientStream.FlushAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.Debug($"ManagedProxyForwarder [{_proxyName}] client: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 处理 CONNECT 隧道（HTTPS）：向上游代理发送 CONNECT 请求（带 Basic 认证），
    /// 收到 200 后双向 relay。若上游返回 407，则重新发送带认证头的请求。
    /// </summary>
    private async Task HandleConnectAsync(NetworkStream clientStream, string target, CancellationToken token)
    {
        using var upstream = new TcpClient();
        await upstream.ConnectAsync(_upstreamHost,
            _upstreamPort, token).ConfigureAwait(false);
        await using var upstreamStream = upstream.GetStream();

        var connectReq = BuildConnectRequest(target, addAuth: !string.IsNullOrEmpty(_username));
        var connectBytes = Encoding.ASCII.GetBytes(connectReq);
        await upstreamStream.WriteAsync(connectBytes, token).ConfigureAwait(false);

        var respBuf = new byte[4096];
        var respLen = await upstreamStream.ReadAsync(respBuf.AsMemory(0, respBuf.Length), token).ConfigureAwait(false);
        var respText = Encoding.ASCII.GetString(respBuf, 0, respLen);

        // P3-2: 精确解析 HTTP 状态码
        var statusCode = ParseHttpStatusCode(respText);

        // 407 挑战-响应：若预认证被拒绝，发送带完整 Basic 认证的 CONNECT
        if (statusCode == 407 && !string.IsNullOrEmpty(_username))
        {
            var authBytes = System.Text.Encoding.UTF8.GetBytes($"{_username}:{_password}");
            var basicAuth = "Basic " + System.Convert.ToBase64String(authBytes);

            var retryReq = new StringBuilder();
            retryReq.Append(CultureInfo.InvariantCulture, $"CONNECT {target} HTTP/1.1\r\n");
            retryReq.Append(CultureInfo.InvariantCulture, $"Host: {target}\r\n");
            retryReq.Append(CultureInfo.InvariantCulture, $"Proxy-Authorization: {basicAuth}\r\n");
            retryReq.Append("\r\n");

            var retryBytes = Encoding.ASCII.GetBytes(retryReq.ToString());
            await upstreamStream.WriteAsync(retryBytes, token).ConfigureAwait(false);
            respLen = await upstreamStream.ReadAsync(respBuf.AsMemory(0, respBuf.Length), token).ConfigureAwait(false);
            respText = Encoding.ASCII.GetString(respBuf, 0, respLen);
            statusCode = ParseHttpStatusCode(respText);
        }

        if (statusCode == 200)
        {
            var ok = "HTTP/1.1 200 Connection Established\r\n\r\n"u8;
            await clientStream.WriteAsync(ok.ToArray(), token).ConfigureAwait(false);

            await Task.WhenAny(
                clientStream.CopyToAsync(upstreamStream, token),
                upstreamStream.CopyToAsync(clientStream, token)
            ).ConfigureAwait(false);
        }
        else
        {
            var err = "HTTP/1.1 502 Bad Gateway\r\n\r\n"u8;
            await clientStream.WriteAsync(err.ToArray(), token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Parses the HTTP status code from the first response line.
    /// </summary>
    private static int ParseHttpStatusCode(string responseText)
    {
        try
        {
            var firstLine = responseText.Split("\r\n")[0];
            var parts = firstLine.Split(' ');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var code))
                return code;
        }
        catch
        {
            // Fall through to default
        }
        return 0;
    }

    private string BuildConnectRequest(string target, bool addAuth)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"CONNECT {target} HTTP/1.1\r\n");
        sb.Append(CultureInfo.InvariantCulture, $"Host: {target}\r\n");

        if (addAuth && _upstreamClient.DefaultRequestHeaders.Authorization is not null)
        {
            sb.Append("Proxy-Authorization: ");
            sb.Append(_upstreamClient.DefaultRequestHeaders.Authorization.Scheme);
            sb.Append(' ');
            sb.Append(_upstreamClient.DefaultRequestHeaders.Authorization.Parameter);
            sb.Append("\r\n");
        }

        sb.Append("\r\n");
        return sb.ToString();
    }

    private async Task WriteError(NetworkStream stream, int code, string msg)
    {
        var resp = Encoding.ASCII.GetBytes($"HTTP/1.1 {code} {msg}\r\nContent-Length: 0\r\n\r\n");
        try { await stream.WriteAsync(resp).ConfigureAwait(false); }
        catch (Exception ex)
        {
            _logger.Debug($"Failed to write error response to client: {ex.Message}");
        }
    }

    public void Stop()
    {
        try { _cts.Cancel(); }
        catch (ObjectDisposedException) { return; } // P3-8: break on disposed
        catch (Exception ex)
        {
            // Ignore cancellation errors during shutdown
            _logger.Debug($"CancellationTokenSource.Cancel() failed during cleanup: {ex.Message}");
        }
        try { _listener.Stop(); }
        catch (ObjectDisposedException) { return; } // P3-8: break on disposed
        catch (Exception ex)
        {
            // Ignore listener stop errors during shutdown
            _logger.Debug($"HttpListener.Stop() failed during cleanup: {ex.Message}");
        }
    }

    public bool IsListening
    {
        get
        {
            try
            {
                using var c = new TcpClient
                {
                    SendTimeout = 200,
                    ReceiveTimeout = 200,
                };
                c.Connect(IPAddress.Loopback, LocalPort);
                return true;
            }
            catch { return false; }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Stop();
        _cts.Dispose();
        _upstreamClient.Dispose();
        _concurrencyLimiter.Dispose();
    }
}
