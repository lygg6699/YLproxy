using System.Net;
using System.Net.Sockets;
using System.Text;

namespace YLproxy.Proxy;

public sealed class TransparentCoalescingForwarder : IDisposable
{
    private readonly TcpListener _listener;
    private readonly string _upstreamHost;
    private readonly int _upstreamPort;
    private readonly string? _username;
    private readonly string? _password;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;
    private bool _disposed;

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public TransparentCoalescingForwarder(
        string upstreamHost,
        int upstreamPort,
        string? username,
        string? password)
    {
        _upstreamHost = upstreamHost ?? throw new ArgumentNullException(nameof(upstreamHost));
        _upstreamPort = upstreamPort;
        _username = username;
        _password = password;

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            await using (var clientStream = client.GetStream())
            {
                var requestBytes = await ReadAllAsync(clientStream, cancellationToken).ConfigureAwait(false);
                if (requestBytes.Length == 0)
                    return;

                var transformed = TransformRequest(requestBytes);

                using var upstream = new TcpClient();
                await upstream.ConnectAsync(_upstreamHost, _upstreamPort, cancellationToken).ConfigureAwait(false);
                await using var upstreamStream = upstream.GetStream();

                await upstreamStream.WriteAsync(transformed, cancellationToken).ConfigureAwait(false);
                await upstreamStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                await upstreamStream.CopyToAsync(clientStream, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Forwarder error: {ex.Message}");
        }
    }

    private byte[] TransformRequest(byte[] requestBytes)
    {
        var requestText = Encoding.ASCII.GetString(requestBytes);

        var headerEndIndex = requestText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var headers = headerEndIndex >= 0
            ? requestText[..headerEndIndex]
            : requestText;
        var body = headerEndIndex >= 0
            ? requestText[(headerEndIndex + 4)..]
            : string.Empty;

        headers = RemoveProxyAuthorization(headers);

        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            var encoded = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{_username}:{_password}"));
            headers += $"\r\nProxy-Authorization: Basic {encoded}";

            Console.WriteLine("Upstream authentication injected");
        }

        var result = headers + "\r\n\r\n" + body;
        return Encoding.ASCII.GetBytes(result);
    }

    private static string RemoveProxyAuthorization(string headers)
    {
        var lines = headers.Split(["\r\n"], StringSplitOptions.None);
        var filtered = lines
            .Where(line => !line.StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase))
            .ToList();
        return string.Join("\r\n", filtered);
    }

    private static async Task<byte[]> ReadAllAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        var totalRead = 0;

        while (true)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                break;
            }

            if (read == 0)
                break;

            await ms.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            totalRead += read;

            // Check if we have a complete HTTP request (headers + body)
            var content = Encoding.ASCII.GetString(ms.ToArray(), 0, (int)ms.Length);
            var headerEnd = content.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd >= 0)
            {
                var headersPart = content[..headerEnd];
                var bodyStart = headerEnd + 4;

                // Determine if we need more body data
                var contentLength = 0;
                foreach (var line in headersPart.Split(["\r\n"], StringSplitOptions.None))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(line["Content-Length:".Length..].Trim(), out var cl))
                            contentLength = cl;
                        break;
                    }
                }

                var bodyReceived = content.Length - bodyStart;
                if (bodyReceived >= contentLength)
                    break;

                // Give a short window for the body to arrive (coalescing)
                if (bodyReceived == 0 && contentLength > 0)
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                    continue;
                }
            }
        }

        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try { _cts.Cancel(); } catch { }
        try { _listener.Stop(); } catch { }
        _cts.Dispose();
    }
}
