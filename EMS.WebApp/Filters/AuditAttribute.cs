using EMS.WebApp.Services;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace EMS.WebApp.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class AuditAttribute : ActionFilterAttribute
    {
        public string TableName { get; set; } = "";
        public string ActionType { get; set; } = "";
        public bool LogRequest { get; set; } = true;
        public bool LogResponse { get; set; } = false;

        // CORRECT METHOD: Use OnActionExecutionAsync instead of OnActionExecutedAsync
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!LogRequest && !LogResponse)
            {
                await next();
                return;
            }

            var auditService = context.HttpContext.RequestServices.GetService<IAuditService>();
            if (auditService == null)
            {
                await next();
                return;
            }

            try
            {
                var controller = context.Controller.GetType().Name.Replace("Controller", "");
                var action = context.ActionDescriptor.RouteValues["action"] ?? "";
                var tableName = !string.IsNullOrEmpty(TableName) ? TableName : controller.ToLower();
                var actionType = !string.IsNullOrEmpty(ActionType) ? ActionType : action.ToUpper();

                var recordId = GetRecordId(context);
                var additionalInfo = $"Controller: {controller}, Action: {action}";

                if (LogRequest)
                {
                    var requestData = GetRequestData(context);
                    await auditService.LogAsync(tableName, $"{actionType}_REQUEST", recordId, null, requestData, additionalInfo);
                }

                // Execute the action
                var executedContext = await next();

                if (LogResponse && executedContext.Result != null)
                {
                    var responseData = GetResponseData(executedContext);
                    await auditService.LogAsync(tableName, $"{actionType}_RESPONSE", recordId, null, responseData, additionalInfo);
                }
            }
            catch (Exception ex)
            {
                // Don't throw, just log
                Console.WriteLine($"Audit attribute failed: {ex.Message}");
                await next();
            }
        }

        private string GetRecordId(ActionExecutingContext context)
        {
            // Try to get ID from route values
            if (context.RouteData.Values.TryGetValue("id", out var id))
                return id?.ToString() ?? "unknown";

            // Try to get from request body or query parameters
            var request = context.HttpContext.Request;
            if (request.Query.ContainsKey("id"))
                return request.Query["id"].ToString();

            return "unknown";
        }

        private object GetRequestData(ActionExecutingContext context)
        {
            var request = context.HttpContext.Request;
            return new
            {
                Method = request.Method,
                Path = request.Path.Value,
                QueryString = request.QueryString.Value,
                Headers = request.Headers.Where(h => !h.Key.StartsWith("Authorization")).ToDictionary(h => h.Key, h => h.Value.ToString())
            };
        }

        private object GetResponseData(ActionExecutedContext context)
        {
            return new
            {
                StatusCode = context.HttpContext.Response.StatusCode,
                ResultType = context.Result?.GetType().Name,
                Success = context.HttpContext.Response.StatusCode >= 200 && context.HttpContext.Response.StatusCode < 300
            };
        }
    }
}
