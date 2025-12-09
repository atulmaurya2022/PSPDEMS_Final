using System.Security.Claims;

namespace EMS.WebApp.Extensions
{
    public static class AuthUserExtensions
    {
        public static string? GetFullName(this ClaimsPrincipal user)
            => user?.FindFirst("FullName")?.Value;

        public static string? GetLoginId(this ClaimsPrincipal user)
            => user?.FindFirst("LoginId")?.Value;

        public static string? GetSessionToken(this ClaimsPrincipal user)
            => user?.FindFirst("SessionToken")?.Value;
    }
}

