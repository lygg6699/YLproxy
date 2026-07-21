using System.Net;
using System.Net.Sockets;
using System.Text;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Proxy;
using System.Runtime.Versioning;


#pragma warning disable CA2022 // E2E test

namespace YLproxy.Tests;

/// <summary>
/// Tests for ManagedProxyForwarder P2-5 streaming (replacing 64KB buffer limit)
/// and P3-1 SemaphoreSlim concurrency limit.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "E2E")]
public sealed class ManagedProxyForwarderStreamTests
{
    /// <summary>
    /// Verifies forwarder can handle a response body larger than 64KB
    /// (testing that streaming is properly implemented).
    /// </summary>
    [Fact]
    public async Task Forwarder_ShouldHandleLargeBody_WithStreaming()
    {
        var localPort = GetFreePort();
        var upstreamPort = GetFreePort();

        using var upstream = new TcpListener(IPAddress.Loopback, upstreamPort);
        upstream.Start();

        var largeBody = new string('X', 80 * 1024); // 80KB > 64KB
        var response = $"HTTP/1.1 200 OK\r\nContent-Length: {largeBody.Length}\r\n\r\n{largeBody}";

        _ = Task.Run(async () =>
        {
            using var client = await upstream.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var buf = new byte[1];
            await stream.ReadAsync(buf.AsMemory(0, buf.Length));
            var respBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(respBytes.AsMemory(0, respBytes.Length));
        });

        var proxy = new ProxyItem
        {
            Id = 901, Name = "stream-test", RemoteHost = "127.0.0.1", RemotePort = upstreamPort,
            Username = "", Password = "", LocalHost = "127.0.0.1", LocalPort = localPort,
        };

        using var forwarder = new ManagedProxyForwarder(proxy);
        forwarder.Start();
        await Task.Delay(200);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, localPort);
        await using var stream = client.GetStream();

        var req = "GET http://example.com/large HTTP/1.1\r\nHost: example.com\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(req));

        // Read entire response
        var buf = new byte[1024 * 1024]; // 1MB buffer
        var totalRead = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            while (totalRead < buf.Length)
            {
                var r = await stream.ReadAsync(buf.AsMemory(totalRead, buf.Length - totalRead), cts.Token);
                if (r <= 0) break;
                totalRead += r;

                // Check if we got the full body
                if (totalRead >= response.Length) break;
            }
        }
        catch (OperationCanceledException)
        {
        }

        var respText = Encoding.ASCII.GetString(buf, 0, totalRead);
        Assert.Contains("200 OK", respText);
        Assert.Contains("XXXXXXXX", respText);
        // Verify we got at least 64KB - the body should be fully transferred
        Assert.True(totalRead > 64 * 1024, $"Expected >64KB response, got {totalRead} bytes");

        forwarder.Stop();
        forwarder.Dispose();
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

