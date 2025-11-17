using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class TestEncryptionController : Controller
    {
        private readonly IConnectionStringEncryptionService _connectionEncryptionService;

        public TestEncryptionController(IConnectionStringEncryptionService connectionEncryptionService)
        {
            _connectionEncryptionService = connectionEncryptionService;
        }

        [HttpGet]
        public IActionResult EncryptConnectionString()
        {
            try
            {
                // Your current connection string
                var originalConnectionString = "Server=10.35.14.204,50001;Initial Catalog=PSPDEMS;User ID=PSPDEMSUser;Password=Pspd@2025;TrustServerCertificate=True;MultipleActiveResultSets=true";

                // Encrypt it
                var encrypted = _connectionEncryptionService.EncryptConnectionString(originalConnectionString);

                // Test decryption
                var decrypted = _connectionEncryptionService.DecryptConnectionString(encrypted);

                return Json(new
                {
                    Success = true,
                    Original = originalConnectionString,
                    Encrypted = encrypted,
                    Decrypted = decrypted,
                    TestPassed = originalConnectionString == decrypted,
                    Instructions = "Copy the 'Encrypted' value above and paste it into your appsettings.json DefaultConnection"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }
    }
}