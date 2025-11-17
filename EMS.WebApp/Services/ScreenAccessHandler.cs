using EMS.WebApp.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace EMS.WebApp.Services
{
    public class ScreenAccessHandler : AuthorizationHandler<ScreenAccessRequirement>
    {
        private readonly IScreenAccessRepository _repository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ScreenAccessHandler> _logger;
        public ScreenAccessHandler(IScreenAccessRepository repository, IHttpContextAccessor httpContextAccessor, ILogger<ScreenAccessHandler> logger)
        {
            _repository = repository;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ScreenAccessRequirement requirement)
        {
            var userName = context.User.Identity?.Name;
            _logger.LogInformation($"=== SCREEN ACCESS CHECK ===");
            _logger.LogInformation($"User: '{userName}'");
            _logger.LogInformation($"Required Screen: '{requirement.ScreenName}'");

            if (string.IsNullOrEmpty(userName))
            {
                _logger.LogWarning("User name is null or empty - FAILING");
                context.Fail();
                return;
            }

            try
            {
                var hasAccess = await _repository.HasScreenAccessAsync(userName, requirement.ScreenName);
                _logger.LogInformation($"HasScreenAccessAsync result: {hasAccess}");

                if (hasAccess)
                {
                    _logger.LogInformation("ACCESS GRANTED");
                    context.Succeed(requirement);
                }
                else
                {
                    _logger.LogWarning("ACCESS DENIED - User does not have permission");
                    context.Fail();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking screen access");
                context.Fail();
            }

            _logger.LogInformation($"=== END SCREEN ACCESS CHECK ===");
        }
    }
}
