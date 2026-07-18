using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using YLproxy.Infrastructure;
using YLproxy.Proxy;

namespace YLproxy.Tests;

[Trait("Category", "Integration")]
public sealed class TransparentCoalescingForwarderTests
{
    [Fact]
    public async Task ForwarderShouldAllocateDynamicPortAndReleaseItOnDispose()
    {
        using var upstream = new TcpListener(IPAddress.Loopback, 0);
        upstream.Start();
        var upstreamPort = ((IPEndPoint)upstream.LocalEndpoint).Port;
        var forwarder = new TransparentCoalescingForwarder("127.0.0.1", upstreamPort, null, null);
        var port = forwarder.Port;

        Assert.InRange(port, 1, 65535);
        await WaitForPortAsync(port, TimeSpan.FromSeconds(3));

        forwarder.Dispose();
        upstream.Stop();
        await WaitForPortReleaseAsync(port, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task ForwarderShouldCoalesceSplitHeaderAndBodyIntoSingleUpstreamWrite()
    {
        using var upstream = new TcpListener(IPAddress.Loopback, 0);
        upstream.Start();
        var upstreamPort = ((IPEndPoint)upstream.LocalEndpoint).Port;
        using var forwarder = new TransparentCoalescingForwarder("127.0.0.1", upstreamPort, "user", "password");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var upstreamTask = AcceptSingleReadAsync(upstream, cancellation.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, forwarder.Port, cancellation.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes("POST http://example.invalid/submit HTTP/1.1\r\nHost: example.invalid\r\n"), cancellation.Token);
        await Task.Delay(50, cancellation.Token);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("Content-Length: 4\r\n\r\nbody"), cancellation.Token);
        await stream.FlushAsync(cancellation.Token);

        var upstreamBytes = await upstreamTask.WaitAsync(TimeSpan.FromSeconds(5));
        var upstreamRequest = Encoding.ASCII.GetString(upstreamBytes);

        Assert.Contains("POST http://example.invalid/submit HTTP/1.1", upstreamRequest, StringComparison.Ordinal);
        Assert.Contains("\r\n\r\nbody", upstreamRequest, StringComparison.Ordinal);
        Assert.Contains("Proxy-Authorization: Basic dXNlcjpwYXNzd29yZA==", upstreamRequest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\r\n\r\nProxy-Authorization", upstreamRequest, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForwarderShouldReplaceExistingAuthorizationAndAvoidCredentialLogs()
    {
        using var upstream = new TcpListener(IPAddress.Loopback, 0);
        upstream.Start();
        var upstreamPort = ((IPEndPoint)upstream.LocalEndpoint).Port;
        using var output = new StringWriter();
        var testLogger = new StringWriterLogger(output);
        using var forwarder = new TransparentCoalescingForwarder("127.0.0.1", upstreamPort, "new-user", "new-password", logger: testLogger);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var upstreamTask = AcceptSingleReadAsync(upstream, cancellation.Token);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, forwarder.Port, cancellation.Token);
            await using var stream = client.GetStream();
            var request = "GET http://example.invalid/ HTTP/1.1\r\nHost: example.invalid\r\nProxy-Authorization: Basic b2xkOmNyZWRz\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(request), cancellation.Token);
            await stream.FlushAsync(cancellation.Token);

            var upstreamRequest = Encoding.ASCII.GetString(await upstreamTask.WaitAsync(TimeSpan.FromSeconds(5)));
            var logs = output.ToString();

            Assert.DoesNotContain("b2xkOmNyZWRz", upstreamRequest, StringComparison.Ordinal);
            Assert.Contains("Proxy-Authorization: Basic bmV3LXVzZXI6bmV3LXBhc3N3b3Jk", upstreamRequest, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bmV3LXVzZXI6bmV3LXBhc3N3b3Jk", logs, StringComparison.Ordinal);
            Assert.DoesNotContain("new-password", logs, StringComparison.Ordinal);
            Assert.Contains("Upstream authentication injected", logs, StringComparison.Ordinal);
        }
        finally
        {
        }
    }

    [Fact]
    public async Task ForwarderShouldInjectAuthHeaderEvenIfIncomingRequestHasNoAuth()
    {
        using var upstream = new TcpListener(IPAddress.Loopback, 0);
        upstream.Start();
        var upstreamPort = ((IPEndPoint)upstream.LocalEndpoint).Port;
        using var forwarder = new TransparentCoalescingForwarder("127.0.0.1", upstreamPort, "user", "password");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var upstreamTask = AcceptSingleReadAsync(upstream, cancellation.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, forwarder.Port, cancellation.Token);
        await using var stream = client.GetStream();
        var request = "GET http://example.invalid/ HTTP/1.1\r\nHost: example.invalid\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), cancellation.Token);
        await stream.FlushAsync(cancellation.Token);

        var upstreamRequest = Encoding.ASCII.GetString(await upstreamTask.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Contains("GET http://example.invalid/ HTTP/1.1", upstreamRequest, StringComparison.Ordinal);
        Assert.Contains("Proxy-Authorization: Basic dXNlcjpwYXNzd29yZA==", upstreamRequest, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<byte[]> AcceptSingleReadAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var upstreamClient = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var upstreamStream = upstreamClient.GetStream();
        upstreamStream.ReadTimeout = 500;
        var buffer = new byte[16 * 1024];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await upstreamStream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (read <= 0) break;
            total += read;
            // 短延迟后若无更多数据则退出，处理 TCP 分片
            await Task.Delay(80, cancellationToken);
            if (!upstreamStream.DataAvailable) break;
        }
        return buffer[..total];
    }

    private static async Task WaitForPortAsync(int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(50);
            }
        }

        throw new TimeoutException($"Port {port} did not start listening within {timeout}.");
    }

    private static async Task WaitForPortReleaseAsync(int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(50);
            }
        }

        throw new TimeoutException($"Port {port} was not released within {timeout}.");
    }
}

internal sealed class StringWriterLogger : ILogger
{
    private readonly StringWriter _writer;
    public StringWriterLogger(StringWriter writer) => _writer = writer;
    public void Debug(string message) => _writer.WriteLine(message);
    public void Info(string message) => _writer.WriteLine(message);
    public void Warn(string message) => _writer.WriteLine(message);
    public void Error(string message) => _writer.WriteLine(message);
    public void Fatal(string message) => _writer.WriteLine(message);
    public void Debug(string message, Exception exception) => _writer.WriteLine($"{message}: {exception.Message}");
    public void Info(string message, Exception exception) => _writer.WriteLine($"{message}: {exception.Message}");
    public void Warn(string message, Exception exception) => _writer.WriteLine($"{message}: {exception.Message}");
    public void Error(string message, Exception exception) => _writer.WriteLine($"{message}: {exception.Message}");
    public void Fatal(string message, Exception exception) => _writer.WriteLine($"{message}: {exception.Message}");
    public void Log(LogLevel level, string message, object? data = null) => _writer.WriteLine(message);
    public void Log(LogLevel level, string message, Exception exception, object? data = null) => _writer.WriteLine($"{message}: {exception.Message}");
}
