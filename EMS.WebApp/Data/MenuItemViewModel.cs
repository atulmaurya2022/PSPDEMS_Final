namespace EMS.WebApp.Data
{
    public class MenuItemViewModel
    {
        public string ScreenName { get; set; } = null!;
        public string ControllerName { get; set; } = null!;
        public string ActionName { get; set; } = "Index"; // default
        public string MenuGroup { get; set; } = "Masters"; // optional
    }
}
