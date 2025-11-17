// File: Services/IEmailService.cs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface IEmailService
    {
        Task<string> SendTestAsync(IEnumerable<string> to, IEnumerable<string> cc, string subject, string htmlBody);
    }
}
