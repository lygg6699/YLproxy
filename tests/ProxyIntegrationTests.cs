using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Proxy;
using YLproxy.Utils;

namespace YLproxy.Tests;

[Trait("Category", "Integration")]
public sealed class ProxyIntegrationTests
{
    [Fact]
    public async Task ProxyProcess_ShouldForwardThroughAuthenticatedHttpParentAndCleanConfig()
    {
        var exePath = PathResolver.ResolvePath("runtime", "3proxy", "bin64", "3proxy.exe");
        if (!File.Exists(exePath))
            return; // 3proxy runtime not available

        using var upstream = new TcpListener(IPAddress.Loopback, 0);
        upstream.Start();
        var upstreamPort = ((IPEndPoint)upstream.LocalEndpoint).Port;
        var localPort = GetFreePort();
        var proxyId = Random.Shared.Next(100_000, 900_000);
        const string username = "e2e-user";
        const string password = "e2e-password";

        var proxy = new ProxyItem
        {
            Id = proxyId,
            Name = "Local E2E",
            RemoteHost = "127.0.0.1",
            RemotePort = upstreamPort,
            Username = username,
            Password = password,
            LocalHost = "127.0.0.1",
            LocalPort = localPort,
            Status = ProxyStatus.Stopped,
            CreateTime = DateTime.UtcNow,
        };

        var configPath = PathResolver.ResolvePath("runtime", "3proxy", "cfg", $"{proxyId}.cfg");
        var logPath = PathResolver.ResolvePath("runtime", "3proxy", "logs", $"3proxy-{proxyId}.log");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var upstreamRequestTask = AcceptRequestAsync(upstream, cancellation.Token);
        var configRemainedAfterStop = false;

        ProxyProcessManager.Configure(new ThreeProxyConfig());

        try
        {
            ProxyProcessManager.Start(proxy);
            Assert.True(File.Exists(configPath) || IsPortListening(localPort, TimeSpan.FromSeconds(3)),
                "cfg 或转发器应在启动后可用");
            await WaitForPortAsync(localPort, TimeSpan.FromSeconds(5));

            using var handler = new HttpClientHandler
            {
                Proxy = new WebProxy($"http://127.0.0.1:{localPort}"),
                UseProxy = true,
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10),
            };

            using var response = await client.GetAsync("http://e2e.invalid/health", cancellation.Token);
            var body = await response.Content.ReadAsStringAsync(cancellation.Token);
            var upstreamRequest = await upstreamRequestTask.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("YLproxy E2E OK", body);
            Assert.Contains("GET http://e2e.invalid/health HTTP/1.1", upstreamRequest, StringComparison.Ordinal);
            Assert.Contains(
                $"Proxy-Authorization: Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"))}",
                upstreamRequest,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            ProxyProcessManager.Stop(proxy);
            configRemainedAfterStop = File.Exists(configPath);
            cancellation.Cancel();
            upstream.Stop();

            try
            {
                await upstreamRequestTask;
            }
            catch
            {
            }

            if (File.Exists(configPath))
                File.Delete(configPath);
            if (File.Exists(logPath))
                File.Delete(logPath);
        }

        Assert.False(configRemainedAfterStop, "ProxyProcessManager.Stop should delete the generated credential-bearing cfg file.");
    }

    private static async Task<string> AcceptRequestAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        using var requestBuffer = new MemoryStream();
        var buffer = new byte[4096];

        while (requestBuffer.Length < 1024 * 1024)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
                break;

            requestBuffer.Write(buffer, 0, read);
            var requestText = Encoding.ASCII.GetString(requestBuffer.ToArray());
            if (requestText.Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                var responseBody = "YLproxy E2E OK";
                var response = $"HTTP/1.1 200 OK\r\nContent-Length: {Encoding.UTF8.GetByteCount(responseBody)}\r\nConnection: close\r\n\r\n{responseBody}";
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(responseBytes.AsMemory(), cancellationToken);
                await stream.FlushAsync(cancellationToken);
                return requestText;
            }
        }

        throw new InvalidOperationException("The upstream HTTP proxy did not receive a complete request.");
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
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
                await Task.Delay(100);
            }
        }

        throw new TimeoutException($"Port {port} did not start listening within {timeout}.");
    }

    private static bool IsPortListening(int port, TimeSpan timeout)
    {
        try
        {
            using var client = new TcpClient();
            if (client.ConnectAsync(IPAddress.Loopback, port).Wait(timeout))
                return true;
        }
        catch (Exception ex)
        {
            // Log connection failure but continue to return false
        }
        return false;
    }
}
