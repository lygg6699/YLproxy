using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace YLproxy.Core;

public static class ProxyTester
{
    private const int MaxRetries = 2;
    private const int RetryDelayMs = 1000;

    /// <summary>
    /// 代理连通性测试使用的目标 URL（可配置回退）。
    /// </summary>
    public static string TestUrl { get; set; } = "https://www.baidu.com";

    /// <summary>
    /// 代理连通性测试，内置重试 + 指数退避（延迟 1s → 2s，最多 3 次尝试）。
    /// </summary>
    public static async Task<(bool Success, long LatencyMs, string? Error)> TestAsync(
        string host,
        int port,
        string? username,
        string? password)
    {
        string? lastError = null;

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var (success, latency, error) = await TestOnceAsync(host, port, username, password);
            if (success) return (true, latency, null);

            lastError = error;

            if (attempt < MaxRetries)
            {
                // 指数退避：1s, 2s
                var delay = RetryDelayMs * (1 << attempt);
                await Task.Delay(delay).ConfigureAwait(false);
            }
        }

        return (false, 0, lastError ?? "连接失败（已重试）");
    }

    private static async Task<(bool Success, long LatencyMs, string? Error)> TestOnceAsync(
        string host,
        int port,
        string? username,
        string? password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(host))
                return (false, 0, "host 为空");

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            var hasUser = !string.IsNullOrEmpty(username);
            var hasPass = !string.IsNullOrEmpty(password);

            // If proxy does not require auth, username/password left empty => use default proxy settings.
            if (hasUser || hasPass)
            {
                if (!(hasUser && hasPass))
                    return (false, 0, "代理认证信息不完整");

                handler.Proxy = new WebProxy($"http://{host}:{port}")
                {
                    Credentials = new NetworkCredential(username!, password!)
                };
                handler.UseProxy = true;
            }
            else
            {
                handler.Proxy = new WebProxy($"http://{host}:{port}");
                handler.UseProxy = true;
            }

            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            // Avoid chunked issues; just request a simple GET.
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

            var sw = Stopwatch.StartNew();
            using var response = await client.GetAsync(TestUrl).ConfigureAwait(false);
            sw.Stop();

            return (response.IsSuccessStatusCode, sw.ElapsedMilliseconds, null);
        }
        catch (TaskCanceledException ex)
        {
            // Timeout manifests as TaskCanceledException in HttpClient.
            return (false, 0, ex.InnerException?.Message ?? "连接失败: 超时");
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

