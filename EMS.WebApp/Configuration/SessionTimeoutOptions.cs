namespace EMS.WebApp.Configuration
{
    public class SessionTimeoutOptions
    {
        public const string SectionName = "SessionTimeout";

        public int TimeoutMinutes { get; set; } = 10;
        public int WarningMinutes { get; set; } = 2;
        public int CheckIntervalSeconds { get; set; } = 30;

        public TimeSpan TimeoutDuration => TimeSpan.FromMinutes(TimeoutMinutes);
        public TimeSpan WarningDuration => TimeSpan.FromMinutes(WarningMinutes);
        public TimeSpan CheckInterval => TimeSpan.FromSeconds(CheckIntervalSeconds);
    }
}
