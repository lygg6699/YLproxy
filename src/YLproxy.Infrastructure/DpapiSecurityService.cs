using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace YLproxy.Infrastructure;

[SupportedOSPlatform("windows")]
public sealed class DpapiSecurityService : ISecurityService
{
    public const string Prefix = "dpapi:v1:";

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        if (plainText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            if (IsEncrypted(plainText))
                return plainText;

            // Invalid payload format: log debug info and treat it as plain text to re-encrypt.
            System.Diagnostics.Debug.WriteLine($"[DpapiSecurityService] Payload format invalid for '{plainText}', re-encrypting as plain text.");
            // Fall through to encryption logic below
        }

        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plainText),
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText) || !encryptedText.StartsWith(Prefix, StringComparison.Ordinal))
            return encryptedText;

        try
        {
            var payload = Convert.FromBase64String(encryptedText[Prefix.Length..]);
            var plainBytes = ProtectedData.Unprotect(
                payload,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or PlatformNotSupportedException)
        {
            throw new CryptographicException("The DPAPI credential payload could not be decrypted for the current Windows user.", ex);
        }
    }

    public bool IsEncrypted(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var payload = text[Prefix.Length..];
        if (payload.Length == 0)
            return false;

        try
        {
            return Convert.FromBase64String(payload).Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
