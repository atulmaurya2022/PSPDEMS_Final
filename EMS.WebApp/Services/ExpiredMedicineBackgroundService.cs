using EMS.WebApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace EMS.WebApp.Services
{
    public class ExpiredMedicineBackgroundService : BackgroundService
    {
        private readonly ILogger<ExpiredMedicineBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _period = TimeSpan.FromHours(6); // Run every 6 hours

        public ExpiredMedicineBackgroundService(
            ILogger<ExpiredMedicineBackgroundService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Enhanced Expired Medicine Background Service started with dual-source support.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncExpiredMedicinesAsync();

                    _logger.LogInformation("Enhanced expired medicine sync completed. Next sync in {Period}.", _period);

                    await Task.Delay(_period, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Enhanced Expired Medicine Background Service is stopping.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during enhanced expired medicine sync.");

                    // Wait 30 minutes before retrying on error
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
            }
        }

        private async Task SyncExpiredMedicinesAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var expiredMedicineRepo = scope.ServiceProvider.GetRequiredService<IExpiredMedicineRepository>();
                var auditService = scope.ServiceProvider.GetService<IAuditService>();

                // Sync both Compounder and Store expired medicines
                var allNewExpiredMedicines = await expiredMedicineRepo.DetectNewExpiredMedicinesAsync("System Auto-Sync");

                if (allNewExpiredMedicines.Any())
                {
                    // Group by source type for detailed logging
                    var sourceTypeCounts = allNewExpiredMedicines.GroupBy(m => m.SourceType).ToDictionary(g => g.Key, g => g.Count());

                    // Add all detected expired medicines
                    foreach (var expiredMedicine in allNewExpiredMedicines)
                    {
                        await expiredMedicineRepo.AddAsync(expiredMedicine);
                    }

                    // Log detailed statistics
                    var compounderCount = sourceTypeCounts.GetValueOrDefault("Compounder", 0);
                    var storeCount = sourceTypeCounts.GetValueOrDefault("Store", 0);

                    _logger.LogInformation(
                        "Auto-sync detected and added {TotalCount} new expired medicines: " +
                        "{CompounderCount} from Compounder, {StoreCount} from Store.",
                        allNewExpiredMedicines.Count, compounderCount, storeCount);

                    // Audit log the sync operation
                    if (auditService != null)
                    {
                        await auditService.LogAsync("expired_medicine", "AUTO_SYNC_SUCCESS", "system", null, null,
                            $"Auto-sync completed - Total: {allNewExpiredMedicines.Count}, Compounder: {compounderCount}, Store: {storeCount}");
                    }

                    // Check for critical items (expired > 90 days)
                    var criticalItems = allNewExpiredMedicines.Where(m => (DateTime.Today - m.ExpiryDate).Days > 90).ToList();
                    if (criticalItems.Any())
                    {
                        var criticalSourceCounts = criticalItems.GroupBy(m => m.SourceType).ToDictionary(g => g.Key, g => g.Count());
                        _logger.LogWarning(
                            "ALERT: {CriticalCount} newly detected expired medicines are critical (>90 days overdue): " +
                            "{CriticalCompounder} Compounder, {CriticalStore} Store. Immediate attention required.",
                            criticalItems.Count,
                            criticalSourceCounts.GetValueOrDefault("Compounder", 0),
                            criticalSourceCounts.GetValueOrDefault("Store", 0));

                        // Audit critical items
                        if (auditService != null)
                        {
                            await auditService.LogAsync("expired_medicine", "CRITICAL_DETECTED", "system", null, null,
                                $"Critical expired medicines detected - Count: {criticalItems.Count}, Sources: {string.Join(", ", criticalSourceCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");
                        }
                    }

                    // Log medicine type breakdown
                    var typeBreakdown = allNewExpiredMedicines
                        .Where(m => m.TypeOfMedicine != "Select Type of Medicine")
                        .GroupBy(m => new { m.SourceType, m.TypeOfMedicine })
                        .Select(g => $"{g.Key.SourceType} {g.Key.TypeOfMedicine}: {g.Count()}")
                        .ToList();

                    if (typeBreakdown.Any())
                    {
                        _logger.LogInformation("Medicine type breakdown: {TypeBreakdown}", string.Join(", ", typeBreakdown));
                    }
                }
                else
                {
                    _logger.LogInformation("Auto-sync completed. No new expired medicines detected from either source.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during enhanced expired medicine sync operation.");

                using var scope = _serviceProvider.CreateScope();
                var auditService = scope.ServiceProvider.GetService<IAuditService>();
                if (auditService != null)
                {
                    await auditService.LogAsync("expired_medicine", "AUTO_SYNC_ERROR", "system", null, null,
                        $"Auto-sync failed: {ex.Message}");
                }

                throw;
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Enhanced Expired Medicine Background Service is stopping...");
            await base.StopAsync(stoppingToken);
        }
    }

    // Enhanced configuration class for background service settings
    public class ExpiredMedicineBackgroundServiceOptions
    {
        public const string SectionName = "ExpiredMedicineBackgroundService";

        public bool Enabled { get; set; } = true;
        public int SyncIntervalHours { get; set; } = 6;
        public int RetryDelayMinutes { get; set; } = 30;
        public bool NotifyOnNewExpired { get; set; } = true;
        public bool NotifyOnCriticalItems { get; set; } = true;
        public int CriticalDaysThreshold { get; set; } = 90;
        public string[]? NotificationEmails { get; set; }

        // NEW: Source-specific settings
        public bool SyncCompounderMedicines { get; set; } = true;
        public bool SyncStoreMedicines { get; set; } = true;
        public bool LogSourceTypeBreakdown { get; set; } = true;

        // NEW: Plant-specific settings
        public int[]? RestrictToPlantIds { get; set; } // Restrict sync to specific plants
        public bool SyncAllPlants { get; set; } = true;
    }
}

// Enhanced version with comprehensive configuration support and notifications
namespace EMS.WebApp.Services
{
    public class ConfigurableExpiredMedicineBackgroundService : BackgroundService
    {
        private readonly ILogger<ConfigurableExpiredMedicineBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ExpiredMedicineBackgroundServiceOptions _options;

        public ConfigurableExpiredMedicineBackgroundService(
            ILogger<ConfigurableExpiredMedicineBackgroundService> logger,
            IServiceProvider serviceProvider,
            Microsoft.Extensions.Options.IOptions<ExpiredMedicineBackgroundServiceOptions> options)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Enhanced Expired Medicine Background Service is disabled in configuration.");
                return;
            }

            var period = TimeSpan.FromHours(_options.SyncIntervalHours);
            _logger.LogInformation(
                "Enhanced Expired Medicine Background Service started with {Period} interval. " +
                "Compounder: {CompounderEnabled}, Store: {StoreEnabled}, Plants: {PlantRestriction}",
                period, _options.SyncCompounderMedicines, _options.SyncStoreMedicines,
                _options.SyncAllPlants ? "All" : string.Join(",", _options.RestrictToPlantIds ?? new int[0]));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var syncResults = await SyncExpiredMedicinesAsync();

                    // Handle notifications
                    if (syncResults.Any(r => r.NewItemsCount > 0) && _options.NotifyOnNewExpired)
                    {
                        await SendNotificationAsync(syncResults);
                    }

                    var totalNewItems = syncResults.Sum(r => r.NewItemsCount);
                    var totalCriticalItems = syncResults.Sum(r => r.CriticalItemsCount);

                    _logger.LogInformation(
                        "Enhanced sync completed. Found {TotalNewItems} new expired medicines " +
                        "({CriticalCount} critical). Next sync in {Period}.",
                        totalNewItems, totalCriticalItems, period);

                    await Task.Delay(period, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Enhanced Expired Medicine Background Service is stopping.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during enhanced expired medicine sync.");

                    var retryDelay = TimeSpan.FromMinutes(_options.RetryDelayMinutes);
                    _logger.LogInformation("Retrying in {Delay}.", retryDelay);
                    await Task.Delay(retryDelay, stoppingToken);
                }
            }
        }

        private async Task<List<SyncResult>> SyncExpiredMedicinesAsync()
        {
            var syncResults = new List<SyncResult>();

            using var scope = _serviceProvider.CreateScope();
            var expiredMedicineRepo = scope.ServiceProvider.GetRequiredService<IExpiredMedicineRepository>();
            var auditService = scope.ServiceProvider.GetService<IAuditService>();

            // Determine which plants to sync
            var plantsToSync = _options.SyncAllPlants ? new int?[] { null } :
                               _options.RestrictToPlantIds?.Cast<int?>().ToArray() ?? new int?[] { null };

            foreach (var plantId in plantsToSync)
            {
                try
                {
                    // Sync Compounder medicines if enabled
                    if (_options.SyncCompounderMedicines)
                    {
                        var compounderResult = await SyncSourceTypeAsync(expiredMedicineRepo, "Compounder", plantId, auditService);
                        syncResults.Add(compounderResult);
                    }

                    // Sync Store medicines if enabled
                    if (_options.SyncStoreMedicines)
                    {
                        var storeResult = await SyncSourceTypeAsync(expiredMedicineRepo, "Store", plantId, auditService);
                        syncResults.Add(storeResult);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing plant {PlantId}", plantId);

                    if (auditService != null)
                    {
                        await auditService.LogAsync("expired_medicine", "SYNC_PLANT_ERROR", "system", null, null,
                            $"Error syncing plant {plantId}: {ex.Message}");
                    }
                }
            }

            return syncResults;
        }

        private async Task<SyncResult> SyncSourceTypeAsync(
            IExpiredMedicineRepository repo,
            string sourceType,
            int? plantId,
            IAuditService? auditService)
        {
            var detectedBy = $"System Auto-Sync ({sourceType})";
            var newExpiredMedicines = await repo.DetectNewExpiredMedicinesAsync(detectedBy, plantId, sourceType);

            if (newExpiredMedicines.Any())
            {
                foreach (var expiredMedicine in newExpiredMedicines)
                {
                    await repo.AddAsync(expiredMedicine);
                }

                // Count critical items
                var criticalItems = newExpiredMedicines.Where(m =>
                    (DateTime.Today - m.ExpiryDate).Days > _options.CriticalDaysThreshold).ToList();

                if (criticalItems.Any() && _options.NotifyOnCriticalItems)
                {
                    _logger.LogWarning(
                        "CRITICAL: {CriticalCount} critical expired {SourceType} medicines detected for plant {PlantId} " +
                        "(>{Threshold} days overdue). Immediate attention required.",
                        criticalItems.Count, sourceType, plantId, _options.CriticalDaysThreshold);
                }

                // Audit logging
                if (auditService != null)
                {
                    await auditService.LogAsync("expired_medicine", $"AUTO_SYNC_{sourceType.ToUpper()}", "system", null, null,
                        $"Auto-sync {sourceType} - Plant: {plantId}, New: {newExpiredMedicines.Count}, Critical: {criticalItems.Count}");
                }

                return new SyncResult
                {
                    SourceType = sourceType,
                    PlantId = plantId,
                    NewItemsCount = newExpiredMedicines.Count,
                    CriticalItemsCount = criticalItems.Count,
                    SyncTime = DateTime.Now,
                    NewItems = newExpiredMedicines.ToList()
                };
            }

            return new SyncResult
            {
                SourceType = sourceType,
                PlantId = plantId,
                NewItemsCount = 0,
                CriticalItemsCount = 0,
                SyncTime = DateTime.Now,
                NewItems = new List<Data.ExpiredMedicine>()
            };
        }

        private async Task SendNotificationAsync(List<SyncResult> syncResults)
        {
            try
            {
                var totalNewItems = syncResults.Sum(r => r.NewItemsCount);
                var totalCriticalItems = syncResults.Sum(r => r.CriticalItemsCount);
                var syncTime = syncResults.FirstOrDefault()?.SyncTime ?? DateTime.Now;

                // Group results by source type for detailed reporting
                var sourceTypeResults = syncResults
                    .Where(r => r.NewItemsCount > 0)
                    .GroupBy(r => r.SourceType)
                    .ToDictionary(g => g.Key, g => new {
                        TotalCount = g.Sum(r => r.NewItemsCount),
                        CriticalCount = g.Sum(r => r.CriticalItemsCount),
                        PlantCount = g.Count()
                    });

                _logger.LogInformation(
                    "Notification: {TotalCount} new expired medicines detected ({CriticalCount} critical) at {SyncTime}. " +
                    "Breakdown: {Breakdown}",
                    totalNewItems, totalCriticalItems, syncTime,
                    string.Join(", ", sourceTypeResults.Select(kvp => $"{kvp.Key}: {kvp.Value.TotalCount}")));

                // Send email notifications if configured
                if (_options.NotificationEmails?.Any() == true)
                {
                    _logger.LogInformation("Email notifications would be sent to: {Emails}",
                        string.Join(", ", _options.NotificationEmails));

                    // TODO: Implement email sending logic here
                    // Example: await _emailService.SendExpiredMedicineAlertAsync(syncResults, _options.NotificationEmails);
                }

                // Log to audit service
                using var scope = _serviceProvider.CreateScope();
                var auditService = scope.ServiceProvider.GetService<IAuditService>();
                if (auditService != null)
                {
                    await auditService.LogAsync("expired_medicine", "NOTIFICATION_SENT", "system", null, null,
                        $"Notification sent - Total: {totalNewItems}, Critical: {totalCriticalItems}, Recipients: {_options.NotificationEmails?.Length ?? 0}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notifications for expired medicines.");
            }
        }

        private class SyncResult
        {
            public string SourceType { get; set; } = "";
            public int? PlantId { get; set; }
            public int NewItemsCount { get; set; }
            public int CriticalItemsCount { get; set; }
            public DateTime SyncTime { get; set; }
            public List<Data.ExpiredMedicine> NewItems { get; set; } = new();
        }
    }

    // Extension method to register the service with configuration
    public static class ExpiredMedicineBackgroundServiceExtensions
    {
        public static IServiceCollection AddExpiredMedicineBackgroundService(
            this IServiceCollection services,
            IConfiguration configuration,
            bool useConfigurableVersion = false)
        {
            // Configure options
            services.Configure<ExpiredMedicineBackgroundServiceOptions>(
                configuration.GetSection(ExpiredMedicineBackgroundServiceOptions.SectionName));

            // Register the appropriate service
            if (useConfigurableVersion)
            {
                services.AddHostedService<ConfigurableExpiredMedicineBackgroundService>();
            }
            else
            {
                services.AddHostedService<ExpiredMedicineBackgroundService>();
            }

            return services;
        }
    }
}