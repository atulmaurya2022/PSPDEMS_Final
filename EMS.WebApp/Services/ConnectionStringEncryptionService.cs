namespace EMS.WebApp.Services
{
    public class ConnectionStringEncryptionService : IConnectionStringEncryptionService
    {
        private readonly IEncryptionService _encryptionService;
        private const string CONNECTION_PREFIX = "CONN_ENC:";

        public ConnectionStringEncryptionService(IEncryptionService encryptionService)
        {
            _encryptionService = encryptionService;
        }

        public string EncryptConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty");

            // If already encrypted, return as is
            if (IsConnectionStringEncrypted(connectionString))
                return connectionString;

            try
            {
                // Use your existing encryption service but add connection-specific prefix
                var encrypted = _encryptionService.Encrypt(connectionString);
                return CONNECTION_PREFIX + encrypted;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to encrypt connection string", ex);
            }
        }

        public string DecryptConnectionString(string encryptedConnectionString)
        {
            if (string.IsNullOrEmpty(encryptedConnectionString))
                throw new ArgumentException("Encrypted connection string cannot be null or empty");

            // If not encrypted, return as is
            if (!IsConnectionStringEncrypted(encryptedConnectionString))
                return encryptedConnectionString;

            try
            {
                // Remove connection prefix and decrypt using existing service
                var actualEncrypted = encryptedConnectionString.Substring(CONNECTION_PREFIX.Length);
                var decrypted = _encryptionService.Decrypt(actualEncrypted);
                return decrypted ?? encryptedConnectionString;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to decrypt connection string", ex);
            }
        }

        public bool IsConnectionStringEncrypted(string connectionString)
        {
            return !string.IsNullOrEmpty(connectionString) && connectionString.StartsWith(CONNECTION_PREFIX);
        }

        // Optional: Method to upgrade connection strings to GCM format
        public string UpgradeConnectionStringToGcm(string encryptedConnectionString)
        {
            if (!IsConnectionStringEncrypted(encryptedConnectionString))
                return encryptedConnectionString;

            try
            {
                // Decrypt and re-encrypt to upgrade to GCM
                var decrypted = DecryptConnectionString(encryptedConnectionString);
                return EncryptConnectionString(decrypted);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to upgrade connection string to GCM", ex);
            }
        }
    }
}