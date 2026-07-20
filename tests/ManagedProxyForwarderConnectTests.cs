using System.Net;
using System.Net.Sockets;
using System.Text;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Proxy;

namespace YLproxy.Tests;

[Trait("Category", "Integration")]
public sealed class ManagedProxyForwarderConnectTests
{
    /// <summary>
    /// Simulates upstream proxy returning 407, then 200 on retry with auth.
    /// Verifies ManagedProxyForwarder correctly retries with Proxy-Authorization header.
    /// Note: 407 retry is sent on the same TCP connection, so upstream must read
    /// the retry request on the same stream.
    /// </summary>
    [Fact]
    public async Task Connect_With407Challenge_ShouldRetryWithAuth()
    {
        var localPort = GetFreePort();
        var upstreamPort = GetFreePort();
        var proxyId = Random.Shared.Next(500_000, 900_000);

        var proxy = new ProxyItem
        {
            Id = proxyId,
            Name = "407-test",
            RemoteHost = "127.0.0.1",
            RemotePort = upstreamPort,
            Username = "testuser",
            Password = "testpass",
            LocalHost = "127.0.0.1",
            LocalPort = localPort,
            Status = ProxyStatus.Stopped,
        };

        // Start upstream listener that responds 407 first, then reads retry on same connection
        using var upstream = new TcpListener(IPAddress.Loopback, upstreamPort);
        upstream.Start();

        var upstreamBehavior = new Upstream407ThenOk(upstream);
        var upstreamTask = upstreamBehavior.HandleAsync();

        using var forwarder = new ManagedProxyForwarder(proxy);
        forwarder.Start();

        await Task.Delay(500);

        // Connect to forwarder with a CONNECT request
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, localPort);
        await using var stream = client.GetStream();

        var connectReq = "CONNECT example.com:443 HTTP/1.1\r\nHost: example.com:443\r\n\r\n";
        var reqBytes = Encoding.ASCII.GetBytes(connectReq);
        await stream.WriteAsync(reqBytes);

        // Read response with timeout
        var buf = new byte[4096];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var len = await stream.ReadAsync(buf.AsMemory(0, buf.Length), cts.Token);
            var resp = Encoding.ASCII.GetString(buf, 0, len);

            // Should get 200 Connection Established
            Assert.Contains("200", resp);
            Assert.Contains("Connection Established", resp);

            // Verify upstream received 2 CONNECT requests.
            // The first request may already include Proxy-Authorization (preemptive auth).
            // The second request (after 407) should also include Proxy-Authorization.
            var (firstReq, secondReq) = await upstreamTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Contains("CONNECT example.com", firstReq);
            Assert.Contains("CONNECT example.com", secondReq);
            // Both requests should retry with auth if needed
            Assert.Contains("Proxy-Authorization", secondReq);
        }
        catch (OperationCanceledException)
        {
            Assert.Fail("Timed out waiting for forwarder response");
        }
        finally
        {
            forwarder.Stop();
            forwarder.Dispose();
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class Upstream407ThenOk
    {
        private readonly TcpListener _listener;

        public Upstream407ThenOk(TcpListener listener)
        {
            _listener = listener;
        }

        public async Task<(string First, string Second)> HandleAsync()
        {
            // Accept single connection, handle both 407 and retry on same TCP stream
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();

            // Read first CONNECT request
            var buf = new byte[4096];
            var len = await ReadAllAsync(stream, buf);
            var firstReq = Encoding.ASCII.GetString(buf, 0, len);

            // Send 407 response
            var resp407 = "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"proxy\"\r\nContent-Length: 0\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(resp407));

            // Read second CONNECT request (retry with auth) on same connection
            await Task.Delay(200);
            buf = new byte[4096];
            len = await ReadAllAsync(stream, buf);
            var secondReq = Encoding.ASCII.GetString(buf, 0, len);

            // Send 200 response
            var resp200 = "HTTP/1.1 200 Connection Established\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(resp200));

            return (firstReq, secondReq);
        }

        private static async Task<int> ReadAllAsync(NetworkStream stream, byte[] buffer)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var r = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead));
                if (r <= 0) break;
                totalRead += r;

                // Check if we have complete HTTP headers
                var text = Encoding.ASCII.GetString(buffer, 0, totalRead);
                if (text.Contains("\r\n\r\n"))
                    break;
            }
            return totalRead;
        }
    }
}
