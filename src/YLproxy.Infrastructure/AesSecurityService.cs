using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using YLproxy.Utils;

namespace YLproxy.Infrastructure;

/// <summary>
/// AES-256-GCM 加密实现，用于非 Windows 平台（替代 DPAPI）。
/// 密钥存储在本地密钥文件中，受文件系统 ACL 保护。
/// </summary>
public sealed class AesSecurityService : ISecurityService
{
    public const string Prefix = "aes:v1:";

    private static readonly string DefaultKeyDir = PathResolver.ResolvePath("data");
    private const string DefaultKeyFileName = "aes-key.dat";

    private readonly byte[] _key;
    private static readonly int NonceSize = 12; // 96-bit nonce for GCM
    private static readonly int TagSize = 16;  // 128-bit authentication tag

    /// <summary>
    /// Creates an AesSecurityService, loading or generating a key file at the specified path.
    /// </summary>
    /// <param name="keyFilePath">Path to the AES key file. If null, uses data/aes-key.dat.</param>
    public AesSecurityService(string? keyFilePath = null)
    {
        var path = keyFilePath ?? Path.Combine(DefaultKeyDir, DefaultKeyFileName);

        // Ensure directory exists
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _key = LoadOrCreateKey(path);
    }

    /// <summary>
    /// Creates an AesSecurityService with a provided key (for testing).
    /// </summary>
    internal AesSecurityService(byte[] key)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes (AES-256).", nameof(key));
    }

    private static byte[] LoadOrCreateKey(string path)
    {
        try
        {
            if (File.Exists(path))
                return File.ReadAllBytes(path);
        }
        catch
        {
            // If read fails, generate new key (backup won't help if we can't read)
        }

        var key = new byte[32]; // AES-256
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);

        // Write key file with restricted permissions where possible
        var tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, key);

        try
        {
            // Atomic rename for crash safety
            if (File.Exists(path))
                File.Replace(tempPath, path, null, ignoreMetadataErrors: true);
            else
                File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            // On some platforms Replace/Move may fail; fallback to copy
            File.Copy(tempPath, path, overwrite: true);
            try { File.Delete(tempPath); } catch (Exception ex)
            {
                _logger.Warn($"Failed to delete temporary file {tempPath}: {ex.Message}");
            }
        }

        return key;
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        if (plainText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            if (IsEncrypted(plainText))
                return plainText;

            throw new FormatException("The AES credential payload has an invalid format.");
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        // Generate unique nonce for each encryption
        var nonce = new byte[NonceSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(nonce);

        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Format: aes:v1:<base64(nonce+ciphertext+tag)>
        var combined = new byte[NonceSize + cipherBytes.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(cipherBytes, 0, combined, NonceSize, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, combined, NonceSize + cipherBytes.Length, TagSize);

        return Prefix + Convert.ToBase64String(combined);
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText) || !encryptedText.StartsWith(Prefix, StringComparison.Ordinal))
            return encryptedText;

        try
        {
            var payload = Convert.FromBase64String(encryptedText[Prefix.Length..]);

            if (payload.Length < NonceSize + TagSize)
                throw new FormatException("Encrypted payload is too short.");

            var nonce = new byte[NonceSize];
            var cipherBytes = new byte[payload.Length - NonceSize - TagSize];
            var tag = new byte[TagSize];

            Buffer.BlockCopy(payload, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(payload, NonceSize, cipherBytes, 0, cipherBytes.Length);
            Buffer.BlockCopy(payload, NonceSize + cipherBytes.Length, tag, 0, TagSize);

            var plainBytes = new byte[cipherBytes.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw new CryptographicException("The AES credential payload could not be decrypted.", ex);
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
            var bytes = Convert.FromBase64String(payload);
            return bytes.Length >= NonceSize + TagSize;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
