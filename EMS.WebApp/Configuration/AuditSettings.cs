namespace EMS.WebApp.Configuration
{
    public class AuditSettings
    {
        public bool EnableAuditLog { get; set; } = true;
        public bool LogViewActions { get; set; } = false;
        public bool LogFailedActions { get; set; } = true;
        public int MaxAuditRetentionDays { get; set; } = 365;
        public bool EnableIPAddressLogging { get; set; } = true;
        public bool EnableUserAgentLogging { get; set; } = true;
        public string[] SensitiveFields { get; set; } = { "password", "token", "secret", "key" };
        public string[] ExcludedTables { get; set; } = { "sys_audit_log" };
        public string[] ExcludedControllers { get; set; } = { "Health", "Diagnostics" };
    }
}