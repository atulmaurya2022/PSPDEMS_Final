namespace EMS.WebApp.Services
{
    public interface IEncryptionService
    {
        string Encrypt(string? plainText);
        string? Decrypt(string? cipherText);
        bool IsEncrypted(string? text);
    }
}
