using System.Text.Json;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using YLproxy.Core.Config;
using YLproxy.Infrastructure;

namespace YLproxy.Tests;

[SupportedOSPlatform("windows")]
[Trait("Category", "Unit")]
public sealed class SecurityServiceTests
{
    [Fact]
    public void DpapiSecurityService_ShouldRoundTripWithoutStoringPlaintext()
    {
        if (!OperatingSystem.IsWindows())
            return; // DPAPI is Windows-only

        var service = new DpapiSecurityService();
        const string secret = "legacy-secret-42";

        var encrypted = service.Encrypt(secret);

        Assert.NotEqual(secret, encrypted);
        Assert.StartsWith(DpapiSecurityService.Prefix, encrypted);
        Assert.True(service.IsEncrypted(encrypted));
        Assert.Equal(secret, service.Decrypt(encrypted));
    }

    [Fact]
    public void ProxyDataSerializer_ShouldMigrateLegacyCredentials()
    {
        if (!OperatingSystem.IsWindows())
            return; // DPAPI is Windows-only

        const string legacyJson = """
        {
          "Proxies": [
            {
              "Id": 77,
              "Name": "Legacy",
              "RemoteHost": "127.0.0.1",
              "RemotePort": 8080,
              "Username": "legacy-user",
              "Password": "legacy-password",
              "LocalHost": "127.0.0.1",
              "LocalPort": 9077,
              "Status": 0,
              "CreateTime": "2026-07-15T00:00:00Z"
            }
          ]
        }
        """;

        var serializer = new ProxyDataSerializer();
        var config = serializer.Deserialize(legacyJson, out var requiresMigration);

        Assert.True(requiresMigration);
        Assert.Equal("legacy-user", config.Proxies[0].Username);
        Assert.Equal("legacy-password", config.Proxies[0].Password);

        var encryptedJson = serializer.Serialize(config);
        using var document = JsonDocument.Parse(encryptedJson);
        var storedProxy = document.RootElement.GetProperty("Proxies")[0];
        var storedUsername = storedProxy.GetProperty("Username").GetString();
        var storedPassword = storedProxy.GetProperty("Password").GetString();

        Assert.NotNull(storedUsername);
        Assert.NotNull(storedPassword);
        Assert.StartsWith(DpapiSecurityService.Prefix, storedUsername);
        Assert.StartsWith(DpapiSecurityService.Prefix, storedPassword);
        Assert.DoesNotContain("legacy-user", encryptedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("legacy-password", encryptedJson, StringComparison.Ordinal);

        var roundTripped = serializer.Deserialize(encryptedJson, out var secondMigration);
        Assert.False(secondMigration);
        Assert.Equal("legacy-user", roundTripped.Proxies[0].Username);
        Assert.Equal("legacy-password", roundTripped.Proxies[0].Password);
    }

      [Fact]
      public void DpapiSecurityService_ShouldRejectMalformedProtectedPayload()
      {
        if (!OperatingSystem.IsWindows())
            return; // DPAPI is Windows-only

        var service = new DpapiSecurityService();

        // Encrypt now re-encrypts invalid payloads instead of throwing
        var reEncrypted = service.Encrypt($"{DpapiSecurityService.Prefix}not-base64");
        Assert.StartsWith(DpapiSecurityService.Prefix, reEncrypted);
        Assert.NotEqual($"{DpapiSecurityService.Prefix}not-base64", reEncrypted);
        // Decrypt still throws for invalid base64
        Assert.Throws<CryptographicException>(() => service.Decrypt($"{DpapiSecurityService.Prefix}not-base64"));
      }
}
