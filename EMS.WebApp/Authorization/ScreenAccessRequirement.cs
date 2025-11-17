using Microsoft.AspNetCore.Authorization;

namespace EMS.WebApp.Authorization
{
    public class ScreenAccessRequirement : IAuthorizationRequirement
    {
        public string ScreenName { get; }

        public ScreenAccessRequirement(string screenName)
        {
            ScreenName = screenName;
        }
    }
}
