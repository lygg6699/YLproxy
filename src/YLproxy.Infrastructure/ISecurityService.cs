using System;

namespace YLproxy.Infrastructure
{
    public interface ISecurityService
    {
        string Encrypt(string plainText);
        string Decrypt(string encryptedText);
        bool IsEncrypted(string text);
    }
}