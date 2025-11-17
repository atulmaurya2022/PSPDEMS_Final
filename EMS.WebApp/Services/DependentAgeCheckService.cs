
namespace EMS.WebApp.Services
{
    public class DependentAgeCheckService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DependentAgeCheckService> _logger;
        private readonly TimeSpan _period = TimeSpan.FromHours(24); // Run daily

        public DependentAgeCheckService(
            IServiceProvider serviceProvider,
            ILogger<DependentAgeCheckService> logger)
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
                    await CheckAndDeactivateOverAgeDependents();

                    // Wait for the next execution
                    await Task.Delay(_period, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Service is being stopped
                    _logger.LogInformation("Dependent Age Check Service is stopping.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in Dependent Age Check Service");

                    // Wait a bit before retrying
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
            }
        }

        private async Task CheckAndDeactivateOverAgeDependents()
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IHrEmployeeDependentRepository>();

            try
            {
                _logger.LogInformation("Starting dependent age check process...");

                var deactivatedCount = await repository.DeactivateChildrenOverAgeLimitAsync();

                if (deactivatedCount > 0)
                {
                    _logger.LogInformation($"Deactivated {deactivatedCount} child dependents who exceeded the age limit of 21 years.");
                }
                else
                {
                    _logger.LogInformation("No child dependents found exceeding the age limit.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during dependent age check process");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dependent Age Check Service is stopping...");
            await base.StopAsync(stoppingToken);
        }
    }
}

namespace EMS.WebApp.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDependentAgeCheckService(this IServiceCollection services)
        {
            services.AddHostedService<EMS.WebApp.Services.DependentAgeCheckService>();
            return services;
        }
    }
}
