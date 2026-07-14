using System;
using System.Text;

namespace YLproxy.Infrastructure
{
    public class DpapiSecurityService : ISecurityService
    {
        // Simple placeholder implementation for Phase 1
        // In a real implementation, this would use Windows DPAPI
        
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;
                
            // Simple base64 encoding as placeholder (NOT secure for production)
            // This is just to demonstrate the interface works
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
        }

        public string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return encryptedText;

            try
            {
                // Simple base64 decoding as placeholder
                byte[] bytes = Convert.FromBase64String(encryptedText);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // If decoding fails, return as-is
                return encryptedText;
            }
        }

        public bool IsEncrypted(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            // Simple heuristic: if it's a valid base64 string, treat as potentially encrypted
            return IsBase64String(text);
        }

        private bool IsBase64String(string base64)
        {
            if (string.IsNullOrEmpty(base64) || base64.Length % 4 != 0)
                return false;

            return System.Text.RegularExpressions.Regex.IsMatch(base64, @"^[A-Za-z0-9\+/]*={0,3}$", System.Text.RegularExpressions.RegexOptions.None);
        }
    }
}