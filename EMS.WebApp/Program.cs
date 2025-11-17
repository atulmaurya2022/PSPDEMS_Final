
using EMS.WebApp.Authorization;
using EMS.WebApp.Configuration;
using EMS.WebApp.Controllers;
using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Middleware;
using EMS.WebApp.Services;
using EMS.WebApp.Services.Reports;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Configuration;
using System.Reflection.PortableExecutable;


var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<AuditSettings>(builder.Configuration.GetSection("AuditSettings"));

// Configure Kestrel to remove server header globally
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.AddServerHeader = false;
});

// Add session timeout configuration
builder.Services.Configure<SessionTimeoutOptions>(
    builder.Configuration.GetSection(SessionTimeoutOptions.SectionName));

// Add Connection String Encryption Service
builder.Services.AddScoped<IConnectionStringEncryptionService, ConnectionStringEncryptionService>();

// Modified DbContext registration to use encrypted connection string
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var encryptionService = serviceProvider.GetRequiredService<IEncryptionService>();
    var connectionEncryptionService = new ConnectionStringEncryptionService(encryptionService);

    var encryptedConnectionString = configuration.GetConnectionString("DefaultConnection") ??
                                   throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    var decryptedConnectionString = connectionEncryptionService.DecryptConnectionString(encryptedConnectionString);

    options.UseSqlServer(decryptedConnectionString);
});


builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
builder.Services.ConfigureSecureCookies();

// Repository registrations
builder.Services.AddScoped<IMedDiseaseRepository, MedDiseaseRepository>();
builder.Services.AddScoped<IMedRefHospitalRepository, MedRefHospitalRepository>();
builder.Services.AddScoped<IMedExamCategoryRepository, MedExamCategoryRepository>();
builder.Services.AddScoped<IMedCategoryRepository, MedCategoryRepository>();
builder.Services.AddScoped<IMedBaseRepository, MedBaseRepository>();
builder.Services.AddScoped<IMedMasterRepository, MedMasterRepository>();
builder.Services.AddScoped<IHrEmployeeRepository, HrEmployeeRepository>();
builder.Services.AddScoped<IHrEmployeeDependentRepository, HrEmployeeDependentRepository>();
builder.Services.AddScoped<IMedDiagnosisRepository, MedDiagnosisRepository>();
builder.Services.AddScoped<ISysUserRepository, SysUserRepository>();
builder.Services.AddScoped<ISysRoleRepository, SysRoleRepository>();
builder.Services.AddScoped<ISysAttachScreenRoleRepository, SysAttachScreenRoleRepository>();
builder.Services.AddScoped<IPlantMasterRepository, PlantMasterRepository>();
builder.Services.AddScoped<IDepartmentMasterRepository, DepartmentMasterRepository>();
builder.Services.AddScoped<IMedAmbulanceMasterRepository, MedAmbulanceMasterRepository>();
builder.Services.AddScoped<ISystemScreenMasterRepository, SystemScreenMasterRepository>();
builder.Services.AddScoped<IAccountLoginRepository, AccountLoginRepository>();
builder.Services.AddScoped<IScreenAccessRepository, ScreenAccessRepository>();
builder.Services.AddScoped<IHealthProfileRepository, HealthProfileRepository>();
builder.Services.AddScoped<IStoreIndentRepository, StoreIndentRepository>();
builder.Services.AddScoped<ICompounderIndentRepository, CompounderIndentRepository>();
builder.Services.AddScoped<IDoctorDiagnosisRepository, DoctorDiagnosisRepository>();
builder.Services.AddScoped<IOthersDiagnosisRepository, OthersDiagnosisRepository>();
builder.Services.AddScoped<IExpiredMedicineRepository, ExpiredMedicineRepository>();
// Medical Examination
builder.Services.AddScoped<IMedExaminationResultRepository, MedExaminationResultRepository>();
builder.Services.AddScoped<IMedExaminationApprovalRepository, MedExaminationApprovalRepository>();
// Immunization
builder.Services.AddScoped<IImmunizationRepository, ImmunizationRepository>();

//Dashbaord
builder.Services.AddScoped<IDashboardService, DashboardService>();

builder.Services.AddScoped<IStoreDashboardService, StoreDashboardService>();
builder.Services.AddScoped<ICompounderDashboardService, CompounderDashboardService>();


// Register services
builder.Services.AddDependentAgeCheckService();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IAuthorizationHandler, ScreenAccessHandler>();
builder.Services.AddHostedService<ExpiredMedicineBackgroundService>();
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IMedicalDataMaskingService, MedicalDataMaskingService>();
builder.Services.AddMemoryCache();
// audit services
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditCleanupService, AuditCleanupService>();

// Program.cs or Startup.cs
builder.Services.AddScoped<IDiagnosisCensusReportService, DiagnosisCensusReportService>();
// IStoreIndentRepository already registered in your app — used for GetUserPlantIdAsync

//Email service
builder.Services.AddScoped<IEmailService, EmailService>(); // <-- THIS FIXES THE ERROR

// Background Services
builder.Services.AddHostedService<AuditCleanupBackgroundService>();
builder.Services.AddHttpContextAccessor();
// ADD ERROR HANDLING SERVICE
builder.Services.AddErrorHandling();


//Email Scheduler
builder.Services.AddScoped<IMedCheckReminderService, MedCheckReminderService>();
// Background worker (no Quartz)
builder.Services.AddHostedService<MedCheckReminderWorker>();


// FIXED: Updated authentication configuration to prevent automatic redirects for 401/403
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = false;


    // options.AccessDeniedPath = "/Account/Login";

    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;

    // ADDED: Custom event handlers to control redirect behavior
    options.Events.OnRedirectToLogin = context =>
    {
        // Check if this is an API request
        if (IsApiRequest(context.Request))
        {
            // For API requests, don't redirect - just return 401
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        }

        // Check if user explicitly wants to see error page instead of login
        if (context.Request.Query.ContainsKey("showError") ||
            context.Request.Headers.ContainsKey("X-Show-Error"))
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        }

        // For normal web requests, redirect to login (default behavior)
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        // Check if this is an API request
        if (IsApiRequest(context.Request))
        {
            // For API requests, don't redirect - just return 403
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        }

        // Check if user explicitly wants to see error page
        if (context.Request.Query.ContainsKey("showError") ||
            context.Request.Headers.ContainsKey("X-Show-Error"))
        {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        }

        // CHANGED: Set status code to let status pages middleware handle it
        // instead of redirecting to login
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
})
.AddNegotiate();

builder.Services.AddAuthorization();

// Session configuration with enhanced security
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

//=========================================================================================
var env = builder.Environment.EnvironmentName; // e.g. "Development", "Production"
var antiforgeryConfig = builder.Configuration.GetSection("Antiforgery").GetSection(env);

builder.Services.AddAntiforgery(options =>
{
    if (Enum.TryParse<CookieSecurePolicy>(antiforgeryConfig["SecurePolicy"], out var securePolicy))
    {
        options.Cookie.SecurePolicy = securePolicy;
    }

    if (Enum.TryParse<SameSiteMode>(antiforgeryConfig["SameSite"], out var sameSite))
    {
        options.Cookie.SameSite = sameSite;
    }

    options.Cookie.HttpOnly = true;
});
//========================================================================================
// Data Protection (encryption/signing of TempData cookie)
builder.Services.AddDataProtection()
    .SetApplicationName("EMS.WebApp") // isolates key ring scope
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90)); // rotate keys regularly
// For production, also persist & protect keys (see Section 3)

builder.Services.AddControllersWithViews()
    .AddCookieTempDataProvider(options =>
    {
        options.Cookie.Name = "EMS.WebApp.TempData";
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;   // HTTPS only
        options.Cookie.HttpOnly = true;                            // no JS access
        options.Cookie.SameSite = SameSiteMode.Strict;             // strongest CSRF posture (use Lax if your flows need cross-site navigations)
        options.Cookie.IsEssential = false;                        // require consent; set true only if you must work before consent
    });

// Enforce safe cookie defaults globally too
builder.Services.Configure<CookiePolicyOptions>(o =>
{
    o.HttpOnly = HttpOnlyPolicy.Always;
    o.Secure = CookieSecurePolicy.Always;
    o.MinimumSameSitePolicy = SameSiteMode.Strict;
});


//=========================================================================================
var app = builder.Build();

// ADDED: Helper method to determine if request is an API request
static bool IsApiRequest(HttpRequest request)
{
    return request.Path.StartsWithSegments("/api") ||
           request.Headers["Accept"].Any(h => h != null && h.Contains("application/json")) ||
           request.ContentType?.Contains("application/json") == true;
}

// CRITICAL: Header removal middleware MUST be FIRST
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        // Remove unwanted headers for ALL responses (including static files)
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("Cache-Control");
        context.Response.Headers.Remove("X-AspNet-Version");
        context.Response.Headers.Remove("X-AspNetMvc-Version");

        // Add security headers for all responses
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin");
        context.Response.Headers.Append("X-Permitted-Cross-Domain-Policies", "none");
        context.Response.Headers.Append("Permissions-Policy",
            "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");

        // Content Security Policy - Fixed syntax
        context.Response.Headers.Append("Content-Security-Policy",
            "font-src 'self'; frame-ancestors 'self';");

        // Default cache control
        context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        context.Response.Headers.Append("Pragma", "no-cache");

        // Additional cache control for sensitive pages
        if (context.Request.Path.StartsWithSegments("/Account") ||
            context.Request.Path.StartsWithSegments("/Admin"))
        {
            //context.Response.Headers.Append("Expires", "0");
        }

        return Task.CompletedTask;
    });

    await next();
});

// Dynamic authorization policy registration
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var authorizationOptions = scope.ServiceProvider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

    var screens = context.sys_screen_names.Select(s => s.screen_name).ToList();

    foreach (var screen in screens)
    {
        var policyName = $"Access{screen.Replace(" ", "")}";
        authorizationOptions.AddPolicy(policyName, policy =>
            policy.Requirements.Add(new ScreenAccessRequirement(screen)));
    }
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    // Production error handling
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
    app.UseHsts();
}
else
{
    // Development - FORCE custom error pages for testing
    // Comment out the developer exception page temporarily
    // app.UseDeveloperExceptionPage();

    // Use custom error pages even in development for testing
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
}

// IMPORTANT: Configure static files AFTER header middleware with additional security
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Remove server headers from static files (Bootstrap, CSS, JS)
        ctx.Context.Response.Headers.Remove("Server");
        ctx.Context.Response.Headers.Remove("X-Powered-By");
        ctx.Context.Response.Headers.Remove("X-AspNet-Version");
        ctx.Context.Response.Headers.Remove("X-AspNetMvc-Version");

        // Add security headers for static files
        ctx.Context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        ctx.Context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
        ctx.Context.Response.Headers.Append("Referrer-Policy", "strict-origin");

        // Cache static files for performance (overrides the no-cache from above)
        var path = ctx.Context.Request.Path.Value?.ToLower();
        if (path != null && (path.EndsWith(".css") || path.EndsWith(".js") ||
                           path.EndsWith(".png") || path.EndsWith(".jpg") ||
                           path.EndsWith(".jpeg") || path.EndsWith(".gif") ||
                           path.EndsWith(".ico") || path.EndsWith(".svg")))
        {
            // Cache static assets for 1 year but allow revalidation
            ctx.Context.Response.Headers.Remove("Cache-Control");
            ctx.Context.Response.Headers.Remove("Pragma");
            ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000, immutable");
        }
    }
});

app.UseStaticFiles();
app.UseHttpsRedirection();
//======================================================================================
app.UseHsts();               // enable in production
app.UseCookiePolicy();
//======================================================================================
app.UseRouting();
app.UseAuthentication();
app.UseSession(); // Session before middleware that depends on it

// Custom middleware
app.UseMiddleware<XssValidationMiddleware>();
app.UseMiddleware<SingleSessionMiddleware>();
app.UseMiddleware<HttpMethodsMiddleware>();

app.UseAuthorization();

app.MapControllerRoute(
    name: "error-with-code",
    pattern: "Error/{statusCode:int}",
    defaults: new { controller = "Error", action = "HttpStatusCodeHandler" });

app.MapControllerRoute(
    name: "error-general",
    pattern: "Error",
    defaults: new
    {
        controller = "Error",
        action = "Error"
    });

// Route mappings
app.MapDefaultControllerRoute();

app.MapControllerRoute(
    name: "compounder-indent-reports",
    pattern: "reports/compounder-indent/{action=Index}",
    defaults: new { controller = "CompounderIndentReport" }
);

app.MapControllerRoute(
    name: "compounder-inventory-reports",
    pattern: "reports/compounder-inventory/{action=Index}",
    defaults: new { controller = "CompounderInventoryReport" }
);

app.MapControllerRoute(
    name: "store-indent-report",
    pattern: "store-indent/report",
    defaults: new { controller = "StoreIndentReport", action = "Index" });

app.MapControllerRoute(
    name: "store-inventory-report",
    pattern: "store-inventory/report",
    defaults: new { controller = "StoreInventoryReport", action = "Index" });

app.MapControllerRoute(
    name: "diagnosis-census-report",
    pattern: "diagnosis-census/report",
    defaults: new { controller = "DiagnosisCensusReport", action = "Index" });

// ADD ERROR HANDLING ROUTES
app.MapControllerRoute(
    name: "error",
    pattern: "Error/{statusCode?}",
    defaults: new { controller = "Error", action = "HttpStatusCodeHandler" });

app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}");

app.Run();