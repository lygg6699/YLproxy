using System.Text.Json;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using YLproxy.Core.Config;
using YLproxy.Infrastructure;
using YLproxy.Models;

namespace YLproxy.Tests;

[SupportedOSPlatform("windows")]
public sealed class SecurityServiceTests
{
    [Fact]
    public void DpapiSecurityServiceShouldRoundTripWithoutStoringPlaintext()
    {
        Assert.True(OperatingSystem.IsWindows());

        var service = new DpapiSecurityService();
        const string secret = "legacy-secret-42";

        var encrypted = service.Encrypt(secret);

        Assert.NotEqual(secret, encrypted);
        Assert.StartsWith(DpapiSecurityService.Prefix, encrypted);
        Assert.True(service.IsEncrypted(encrypted));
        Assert.Equal(secret, service.Decrypt(encrypted));
    }

    [Fact]
    public void ProxyDataSerializerShouldMigrateLegacyCredentials()
    {
        Assert.True(OperatingSystem.IsWindows());

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
      public void DpapiSecurityServiceShouldRejectMalformedProtectedPayload()
      {
        Assert.True(OperatingSystem.IsWindows());

        var service = new DpapiSecurityService();

        Assert.Throws<FormatException>(() => service.Encrypt($"{DpapiSecurityService.Prefix}not-base64"));
        Assert.Throws<CryptographicException>(() => service.Decrypt($"{DpapiSecurityService.Prefix}not-base64"));
      }

    [Fact]
    public void ProxyDataSerializerShouldResetCredentialsWhenDecryptionFails()
    {
        var serializer = new ProxyDataSerializer(new FailingSecurityService());
        const string protectedJson = """
        {
          "Proxies": [
            {
              "Id": 88,
              "Name": "Foreign machine",
              "RemoteHost": "127.0.0.1",
              "RemotePort": 8080,
              "Username": "dpapi:v1:foreign-user",
              "Password": "dpapi:v1:foreign-password",
              "LocalHost": "127.0.0.1",
              "LocalPort": 9088,
              "Status": 1,
              "CreateTime": "2026-07-15T00:00:00Z"
            }
          ]
        }
        """;

        var config = serializer.Deserialize(protectedJson, out var requiresMigration, out var credentialsReset);

        Assert.True(requiresMigration);
        Assert.True(credentialsReset);
        Assert.Empty(config.Proxies[0].Username);
        Assert.Empty(config.Proxies[0].Password);
        Assert.Equal(ProxyStatus.Stopped, config.Proxies[0].Status);
    }

    private sealed class FailingSecurityService : ISecurityService
    {
        public string Encrypt(string plainText) => plainText;

        public string Decrypt(string encryptedText)
        {
            if (encryptedText.StartsWith(DpapiSecurityService.Prefix, StringComparison.Ordinal))
                throw new CryptographicException("The payload belongs to another Windows user.");

            return encryptedText;
        }

        public bool IsEncrypted(string text) => text.StartsWith(DpapiSecurityService.Prefix, StringComparison.Ordinal);
    }
}
