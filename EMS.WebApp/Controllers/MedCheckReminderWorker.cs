

using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EMS.WebApp.Controllers
{
    public sealed class MedCheckReminderWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public MedCheckReminderWorker(IServiceScopeFactory scopeFactory)
            => _scopeFactory = scopeFactory;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                DateTime now;
                DateTime nextRun;

                // Read SchedulerHours/SchedulerMinutes from DB each loop
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var cfg = await db.Configurations.FirstOrDefaultAsync(stoppingToken);

                    var hour = (cfg?.ScheduleHours > 0) ? cfg!.ScheduleHours : 1; // default 01:00
                    var minute = (cfg?.ScheduleMinutes > 0) ? cfg!.ScheduleMinutes : 0;

                    now = DateTime.Now;
                    nextRun = new DateTime(now.Year, now.Month, now.Day, (int)hour, (int)minute, 0);

                    if (nextRun <= now) nextRun = nextRun.AddDays(1);
                }

                var delay = nextRun - DateTime.Now;
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException) { break; }

                // Do the actual job
                using var execScope = _scopeFactory.CreateScope();
                var service = execScope.ServiceProvider.GetRequiredService<IMedCheckReminderService>();
                await service.RunAsync(DateTime.Now, stoppingToken);
            }
        }
    }

}
