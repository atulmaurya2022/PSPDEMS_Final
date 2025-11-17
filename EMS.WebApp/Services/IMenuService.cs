using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IMenuService
    {
        Task<List<MenuItemViewModel>> GetMenuItemsForUserAsync(string email);
        Task<Dictionary<string, List<MenuItemViewModel>>> GetGroupedMenuItemsForUserAsync(string fullName);
    }
}