using Microsoft.AspNetCore.SystemWebAdapters;
using MudBlazor.Services;
using SignaturPortal.Application.Authorization;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure;
using SignaturPortal.Infrastructure.Authorization;
using SignaturPortal.Web.Components;
using SignaturPortal.Web.Components.Services;
using SignaturPortal.Web.Endpoints;
using SignaturPortal.Web.Middleware;
using SignaturPortal.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        // Increase max message size for file downloads (10 MB)
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
    });

// Cascading auth state — enables AuthorizeRouteView to enforce [Authorize] during SPA navigation
builder.Services.AddCascadingAuthenticationState();

// MudBlazor
builder.Services.AddMudServices();

// Infrastructure layer - EF Core, repositories, Unit of Work, permissions, auth handler
builder.Services.AddInfrastructure(builder.Configuration);

// Authorization policies based on legacy permission IDs
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RecruitmentAccess", policy =>
        policy.Requirements.Add(new PermissionRequirement((int)PortalPermission.RecruitmentPortalRecruitmentAccess)))
    .AddPolicy("RecruitmentAdmin", policy =>
        policy.Requirements.Add(new PermissionRequirement((int)PortalPermission.RecruitmentPortalAdminAccess)));

// YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Distributed cache - required for session
builder.Services.AddDistributedMemoryCache();

// ASP.NET Core Session - required as base for System.Web Adapters remote session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// System.Web Adapters - Session + Auth sharing with legacy WebForms
builder.Services.AddSystemWebAdapters()
    .AddJsonSessionSerializer(options =>
    {
        // These 5 keys are now established by the Blazor app from the DB (via ICurrentUserService)
        // and no longer need to be read from the legacy shared session.
        //options.RegisterKey<Guid>("UserId");
        //options.RegisterKey<int>("SiteId");
        //options.RegisterKey<int>("ClientId");
        //options.RegisterKey<string>("UserName");
        //options.RegisterKey<int>("UserLanguageId");

        // Legacy session keys still needed from the shared session:

        // SSO login disclaimer flag — set to true after user accepts disclaimer
        // Access in OnInitialized (SSR only — not available during SignalR interactions):
        //   var disclaimerChecked = (bool)(System.Web.HttpContext.Current.Session["SsoLoginDisclaimerChecked"] ?? false);
        options.RegisterKey<bool>("SsoLoginDisclaimerChecked");

        // Impersonation: holds the original admin's UserId (Guid) when impersonating another user.
        // Read during SSR in UserSessionMiddleware → IUserSessionContext.IsImpersonating / ImpersonatedByFullName.
        options.RegisterKey<Guid>("ImpersonatedBy");
    })
    .AddRemoteAppClient(options =>
    {
        options.RemoteAppUrl = new Uri(builder.Configuration["RemoteAppUri"]!);
        options.ApiKey = builder.Configuration["RemoteAppApiKey"]!;
    })
    .AddSessionClient()
    .AddAuthenticationClient(true); // true = set as default auth scheme

// Ignore unknown session keys from legacy app (it has dozens we don't need)
builder.Services.Configure<Microsoft.AspNetCore.SystemWebAdapters.SessionState.Serialization.SessionSerializerOptions>(
    options => options.ThrowOnUnknownSessionKey = false);

// HttpContext accessor for session access in services
builder.Services.AddHttpContextAccessor();

// Scoped session context — caches legacy session values for the Blazor circuit lifetime
builder.Services.AddScoped<IUserSessionContext, UserSessionContext>();

// Navigation config — stateless route-to-nav-config resolution (singleton)
builder.Services.AddSingleton<INavigationConfigService, NavigationConfigService>();

// Theme service — pre-built per-portal MudTheme instances (singleton)
builder.Services.AddSingleton<IThemeService, ThemeService>();

// Dark mode state — per-circuit (scoped), shared between MainLayout and NavMenu
builder.Services.AddScoped<ThemeStateService>();

var app = builder.Build();

// Middleware pipeline - ORDER IS CRITICAL
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseSystemWebAdapters(); // MUST be before UseAuthentication to set up remote auth/session
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<UserSessionMiddleware>(); // Populate scoped session context from DB (after auth so HttpContext.User is available)
app.UseAntiforgery();

// Legacy URL redirects to Blazor routes (controlled by ERActivityListUseLegacyUrls config)
if (app.Configuration.GetValue<bool>("ERActivityListUseLegacyUrls"))
{
    app.MapGet("/Responsive/Recruiting/ActivityList.aspx", (HttpContext context) =>
    {
        // Parse Mode query parameter (1=Ongoing, 2=Closed, 3=Draft)
        var modeParam = context.Request.Query["Mode"].ToString();
        var blazorRoute = modeParam switch
        {
            "2" => "/recruiting/activities/closed",
            "3" => "/recruiting/activities/draft",
            "1" or "" => "/recruiting/activities/ongoing",
            _ => "/recruiting/activities/ongoing" // Default to ongoing
        };

        return Results.Redirect(blazorRoute, permanent: false);
    });
}

// Auth logout — clears session and Forms Auth cookie, then redirects to legacy login page
app.MapGet("/auth/logout", (HttpContext context) =>
{
    context.Session.Clear();
    context.Response.Cookies.Delete(".ASPXAUTH", new CookieOptions { Path = "/" });
    return Results.Redirect("/login.aspx");
});

// Activity Excel export endpoint (before Blazor components, before YARP catch-all)
app.MapActivityExportEndpoints();

// Blazor components (higher precedence - matched first)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireSystemWebAdapterSession(new SessionAttribute { SessionBehavior = System.Web.SessionState.SessionStateBehavior.ReadOnly });

// YARP fallback to WebForms (MUST be last - lowest precedence)
// int.MaxValue ensures this only matches when no Blazor route handles the request
var remoteAppUrl = builder.Configuration["RemoteAppUri"]!;
app.MapForwarder("/{**catch-all}", remoteAppUrl)
    .WithOrder(int.MaxValue)
    .ShortCircuit();

app.Run();
