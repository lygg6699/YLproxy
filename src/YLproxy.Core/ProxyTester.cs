using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace YLproxy.Core;

public static class ProxyTester
{
    private const int DefaultMaxRetries = 2;
    private const int DefaultRetryDelayMs = 1000;

    /// <summary>
    /// 代理连通性测试使用的目标 URL（可配置）。
    /// </summary>
    public static string TestUrl { get; set; } = "https://www.baidu.com";

    /// <summary>
    /// 单次测试超时时间（毫秒），默认 15000ms。
    /// </summary>
    public static int TimeoutMs { get; set; } = 15000;

    /// <summary>
    /// 代理连通性测试，内置重试 + 指数退避。
    /// </summary>
    public static Task<(bool Success, long LatencyMs, string? Error)> TestAsync(
        string host,
        int port,
        string? username,
        string? password,
        CancellationToken cancellationToken = default)
    {
        return TestAsync(host, port, username, password, DefaultMaxRetries, DefaultRetryDelayMs, cancellationToken);
    }

    /// <summary>
    /// 代理连通性测试，可配置重试策略。
    /// </summary>
    /// <param name="maxRetries">最大重试次数（总共尝试 maxRetries + 1 次）。</param>
    /// <param name="retryDelayMs">基础重试延迟（毫秒），每次重试翻倍。</param>
    public static async Task<(bool Success, long LatencyMs, string? Error)> TestAsync(
        string host,
        int port,
        string? username,
        string? password,
        int maxRetries,
        int retryDelayMs,
        CancellationToken cancellationToken = default)
    {
        string? lastError = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (success, latency, error) = await TestOnceAsync(host, port, username, password, cancellationToken);
            if (success) return (true, latency, null);

            lastError = error;

            if (attempt < maxRetries)
            {
                // 指数退避: delay, delay*2, delay*4, ...
                var delay = retryDelayMs * (1 << attempt);
                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return (false, 0, "测试已取消");
                }
            }
        }

        return (false, 0, lastError ?? "连接失败（已重试）");
    }

    private static async Task<(bool Success, long LatencyMs, string? Error)> TestOnceAsync(
        string host,
        int port,
        string? username,
        string? password,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(host))
                return (false, 0, "host 为空");

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
            };

            var hasUser = !string.IsNullOrEmpty(username);
            var hasPass = !string.IsNullOrEmpty(password);

            if (hasUser != hasPass)
                return (false, 0, "代理认证信息不完整");

            handler.Proxy = new WebProxy($"http://{host}:{port}");
            handler.UseProxy = true;

            if (hasUser)
            {
                handler.Proxy.Credentials = new NetworkCredential(username, password);
                // 预认证：避免 407 往返，直接发送 Basic auth
                handler.PreAuthenticate = true;
                handler.DefaultProxyCredentials = handler.Proxy.Credentials;
            }

            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(TimeoutMs)
            };

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

            var sw = Stopwatch.StartNew();
            using var response = await client.GetAsync(TestUrl, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            return (response.IsSuccessStatusCode, sw.ElapsedMilliseconds, null);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, 0, "连接失败: 超时");
        }
        catch (OperationCanceledException)
        {
            return (false, 0, "测试已取消");
        }
        catch (HttpRequestException ex)
        {
            return (false, 0, $"连接失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, 0, $"连接失败: {ex.Message}");
        }
    }
}
