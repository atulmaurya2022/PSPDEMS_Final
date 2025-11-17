namespace EMS.WebApp.Services
{
    public interface IConnectionStringEncryptionService
    {
        string EncryptConnectionString(string connectionString);
        string DecryptConnectionString(string encryptedConnectionString);
        bool IsConnectionStringEncrypted(string connectionString);
    }
}
