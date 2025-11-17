namespace EMS.WebApp.Data
{
    public class ScreenUpdateResult
    {
        public bool Success { get; set; }
        public List<string> AvailableControllers { get; set; } = new();
    }
}
