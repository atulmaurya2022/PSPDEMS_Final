using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class MenuService : IMenuService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MenuService(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<List<MenuItemViewModel>> GetMenuItemsForUserAsync(string fullName)
        {
            var user = await _db.SysUsers.FirstOrDefaultAsync(u => u.adid == fullName && u.is_active);
            if (user == null)
            {
                user = await _db.SysUsers.FirstOrDefaultAsync(u => u.full_name == fullName && u.is_active);
            }
            if (user == null)
            {
                return new List<MenuItemViewModel>();
            }

            var roleId = user.role_id;

            var screens = await _db.sys_screen_names.ToListAsync();
            var mappings = await _db.SysAttachScreenRoles
                                    .Where(m => m.role_uid == roleId)
                                    .ToListAsync();

            var menuItems = mappings
                .SelectMany(m =>
                    m.screen_uid.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(idStr => int.TryParse(idStr, out var id) ? id : 0)
                    .Where(id => id > 0)
                    .Select(screenId =>
                    {
                        var screen = screens.FirstOrDefault(s => s.screen_uid == screenId);
                        return screen != null ? new MenuItemViewModel
                        {
                            ScreenName = screen.screen_name,
                            ControllerName = GetControllerName(screen.screen_name),
                            ActionName = GetActionName(screen.screen_name),
                            MenuGroup = DetermineMenuGroup(screen.screen_name)
                        } : null;
                        //return screen != null ? new MenuItemViewModel
                        //{
                        //    ScreenName = screen.screen_name,
                        //    ControllerName = screen.screen_name,
                        //    ActionName = "Index",
                        //    //ControllerName = screen.screen_name, // <-- use from DB
                        //    //ActionName = screen.action_name,
                        //    MenuGroup = DetermineMenuGroup(screen.screen_name)
                        //} : null;
                    })
                    .Where(mi => mi != null)
                )
                .ToList();

            Console.WriteLine($"Menu count: {menuItems.Count}");

            return menuItems;
        }

        /// <summary>
        /// Determines the menu group based on screen name
        /// </summary>
        /// <param name="screenName">Name of the screen</param>
        /// <returns>Menu group name</returns>
        private string DetermineMenuGroup(string screenName)
        {
            if (string.IsNullOrEmpty(screenName))
                return "Masters";

            var lowerScreenName = screenName.ToLower();

            if (lowerScreenName.Contains("auditlog") ||
                lowerScreenName.Contains("audit_log") ||
                lowerScreenName.Contains("audit log") ||
                lowerScreenName.Contains("log") && lowerScreenName.Contains("audit"))
            {
                return "AuditLog";
            }
            if (lowerScreenName.Contains("report"))
            {
                return "Reports";
            }
            // Transaction-related keywords
            var transactionKeywords = new[]
            {
                "transaction", "expiredmedicine", "doctordiagnosis", "compoundercndent", "storeindent",
                "employeehealthprofile", "othersdiagnosis", "indent", "entry", "examination",
                "bill", "purchase", "medexaminationresult", "order", "delivery",
                "stock", "inventory", "movement", "transfer",
                "cashbook", "bankbook", "immunization"
            };

            // Check if screen name contains transaction keywords
            if (transactionKeywords.Any(keyword => lowerScreenName.Contains(keyword)))
            {
                return "Transactions";
            }

            // Master-related keywords (optional - for explicit categorization)
            var masterKeywords = new[]
            {
                "master", "setup", "config", "user", "role",
                "category", "group", "type", "status", "settings",
                "customer", "vendor", "email",
                "product", "item", "company", "branch", "department",
                "customer", "vendor", "supplier",
                "product", "item", "company", "branch", "department", "emailconfig"

            };

            if (masterKeywords.Any(keyword => lowerScreenName.Contains(keyword)))
            {
                return "Masters";
            }

            // Default to Masters for unmatched screens
            return "Masters";
        }

        /// <summary>
        /// Alternative method: Get menu items grouped by menu type
        /// </summary>
        /// <param name="fullName">User's full name</param>
        /// <returns>Dictionary with menu groups as keys and menu items as values</returns>
        public async Task<Dictionary<string, List<MenuItemViewModel>>> GetGroupedMenuItemsForUserAsync(string fullName)
        {
            var allMenuItems = await GetMenuItemsForUserAsync(fullName);

            return allMenuItems
                .GroupBy(item => item.MenuGroup)
                .ToDictionary(group => group.Key, group => group.ToList());
        }


        private readonly Dictionary<string, (string Controller, string Action)> _reportActionMap =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            { "Compounder Indent Report", ("CompounderIndent", "CompounderIndentReportView") },
            { "Compounder Inventory Report", ("CompounderIndent", "CompounderInventoryReportView") },
            { "Store Indent Report", ("StoreIndent", "StoreIndentReportView") },
            { "Store Inventory Report", ("StoreIndent", "StoreInventoryReportView") },
            // Add more reports as needed
        };
        private string GetControllerName(string screenName)
        {
            if (_reportActionMap.TryGetValue(screenName, out var mapping))
            {
                return mapping.Controller;
            }
            // Default logic: use screenName or whatever you already use for standard screens
            return screenName.Replace(" ", "");
        }

        private string GetActionName(string screenName)
        {
            if (_reportActionMap.TryGetValue(screenName, out var mapping))
            {
                return mapping.Action;
            }
            // Default logic: "Index" or whatever is your default action
            return "Index";
        }
    }
}