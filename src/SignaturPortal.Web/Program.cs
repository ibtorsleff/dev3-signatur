using Microsoft.AspNetCore.SystemWebAdapters;
using MudBlazor.Services;
using SignaturPortal.Application.Authorization;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure;
using SignaturPortal.Infrastructure.Authorization;
using SignaturPortal.Web.Components;
using SignaturPortal.Web.Middleware;
using SignaturPortal.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

// Infrastructure layer - EF Core, repositories, Unit of Work, permissions, auth handler
builder.Services.AddInfrastructure(builder.Configuration);

// Authorization policies based on legacy permission IDs
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RecruitmentAccess", policy =>
        policy.Requirements.Add(new PermissionRequirement((int)ERecruitmentPermission.RecruitmentAccess)))
    .AddPolicy("RecruitmentAdmin", policy =>
        policy.Requirements.Add(new PermissionRequirement((int)ERecruitmentPermission.AdminAccess)));

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
        options.RegisterKey<int>("UserId");
        options.RegisterKey<int>("SiteId");
        options.RegisterKey<int>("ClientId");
        options.RegisterKey<string>("UserName");
        options.RegisterKey<int>("UserLanguageId");
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
app.UseMiddleware<UserSessionMiddleware>(); // Populate scoped session context from legacy session
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

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
