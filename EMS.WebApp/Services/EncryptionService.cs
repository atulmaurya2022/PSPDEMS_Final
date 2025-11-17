using System.Security.Cryptography;
using System.Text;

namespace EMS.WebApp.Services
{
    public class EncryptionService : IEncryptionService
    {
        private readonly byte[] _encryptionKey;
        private const string ENCRYPTION_PREFIX = "ENC:";
        private const string GCM_PREFIX = "GCM:"; // New prefix for GCM encrypted data
        private const int NONCE_SIZE = 12; // 96 bits recommended for GCM
        private const int TAG_SIZE = 16;   // 128 bits authentication tag

        public EncryptionService(IConfiguration configuration)
        {
            // Get encryption key from configuration or use a default one
            string keyString = configuration["EncryptionSettings:Key"] ?? "YourDefaultEncryptionKey2024!@#";

            // Ensure key is exactly 32 bytes for AES-256
            if (keyString.Length < 32)
            {
                keyString = keyString.PadRight(32, '0');
            }
            else if (keyString.Length > 32)
            {
                keyString = keyString.Substring(0, 32);
            }

            _encryptionKey = Encoding.UTF8.GetBytes(keyString);
        }

        public string Encrypt(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            // If already encrypted (either format), return as is
            if (IsEncrypted(plainText))
                return plainText;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

                // Generate random nonce (96 bits for GCM)
                byte[] nonce = new byte[NONCE_SIZE];
                RandomNumberGenerator.Fill(nonce);

                // Prepare buffers
                byte[] cipherBytes = new byte[plainBytes.Length];
                byte[] tag = new byte[TAG_SIZE];

                // Encrypt using AES-GCM
                using (var aesGcm = new AesGcm(_encryptionKey, TAG_SIZE))
                {
                    aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);
                }

                // Combine: nonce + ciphertext + authentication tag
                byte[] result = new byte[nonce.Length + cipherBytes.Length + tag.Length];
                Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
                Buffer.BlockCopy(cipherBytes, 0, result, nonce.Length, cipherBytes.Length);
                Buffer.BlockCopy(tag, 0, result, nonce.Length + cipherBytes.Length, tag.Length);

                // Use new GCM prefix for new encryptions
                return GCM_PREFIX + Convert.ToBase64String(result);
            }
            catch (Exception ex)
            {
                // Log the error and return the original text
                Console.WriteLine($"Encryption error: {ex.Message}");
                return plainText;
            }
        }

        public string? Decrypt(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            // If not encrypted, return as is
            if (!IsEncrypted(cipherText))
                return cipherText;

            try
            {
                // Check if it's GCM format (new) or CBC format (legacy)
                if (cipherText.StartsWith(GCM_PREFIX))
                {
                    return DecryptGcm(cipherText);
                }
                else if (cipherText.StartsWith(ENCRYPTION_PREFIX))
                {
                    return DecryptCbc(cipherText);
                }

                return cipherText;
            }
            catch (Exception ex)
            {
                // Log the error and return the original text
                Console.WriteLine($"Decryption error: {ex.Message}");
                return cipherText;
            }
        }

        private string DecryptGcm(string cipherText)
        {
            // Remove the GCM prefix
            string actualCipherText = cipherText.Substring(GCM_PREFIX.Length);
            byte[] encryptedData = Convert.FromBase64String(actualCipherText);

            // Validate minimum length
            if (encryptedData.Length < NONCE_SIZE + TAG_SIZE)
            {
                throw new ArgumentException("Invalid GCM encrypted data format");
            }

            // Extract components: nonce + ciphertext + tag
            byte[] nonce = new byte[NONCE_SIZE];
            byte[] tag = new byte[TAG_SIZE];
            byte[] cipherBytes = new byte[encryptedData.Length - NONCE_SIZE - TAG_SIZE];

            Buffer.BlockCopy(encryptedData, 0, nonce, 0, NONCE_SIZE);
            Buffer.BlockCopy(encryptedData, NONCE_SIZE, cipherBytes, 0, cipherBytes.Length);
            Buffer.BlockCopy(encryptedData, NONCE_SIZE + cipherBytes.Length, tag, 0, TAG_SIZE);

            // Decrypt using AES-GCM
            byte[] plainBytes = new byte[cipherBytes.Length];
            using (var aesGcm = new AesGcm(_encryptionKey, TAG_SIZE))
            {
                aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }

        private string DecryptCbc(string cipherText)
        {
            // Legacy CBC decryption for backward compatibility
            string actualCipherText = cipherText.Substring(ENCRYPTION_PREFIX.Length);
            byte[] cipherBytes = Convert.FromBase64String(actualCipherText);

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = _encryptionKey; // Use byte array directly
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                // Extract IV from the beginning of the cipher bytes
                byte[] iv = new byte[aesAlg.BlockSize / 8];
                byte[] actualCipherBytes = new byte[cipherBytes.Length - iv.Length];

                Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
                Array.Copy(cipherBytes, iv.Length, actualCipherBytes, 0, actualCipherBytes.Length);

                aesAlg.IV = iv;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(actualCipherBytes))
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }

        public bool IsEncrypted(string? text)
        {
            return !string.IsNullOrEmpty(text) &&
                   (text.StartsWith(ENCRYPTION_PREFIX) || text.StartsWith(GCM_PREFIX));
        }

        // Helper method to check if data uses new GCM format
        public bool IsGcmEncrypted(string? text)
        {
            return !string.IsNullOrEmpty(text) && text.StartsWith(GCM_PREFIX);
        }

        // Helper method to re-encrypt CBC data to GCM format
        public string? UpgradeToGcm(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText) || IsGcmEncrypted(cipherText))
                return cipherText;

            try
            {
                // Decrypt the old format and re-encrypt with new format
                var plainText = Decrypt(cipherText);
                if (plainText == null) return cipherText;

                // Temporarily mark as not encrypted to force re-encryption
                return Encrypt(plainText);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Upgrade to GCM error: {ex.Message}");
                return cipherText;
            }
        }
    }
}