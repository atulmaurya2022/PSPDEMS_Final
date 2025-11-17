using Microsoft.Extensions.Hosting;

namespace EMS.WebApp.Services
{
    public class AuditCleanupBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AuditCleanupBackgroundService> _logger;

        public AuditCleanupBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<AuditCleanupBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var cleanupService = scope.ServiceProvider.GetRequiredService<IAuditCleanupService>();

                    await cleanupService.CleanupOldAuditLogsAsync();

                    // Run cleanup once daily
                    await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in audit cleanup background service");
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
        }
    }
}
