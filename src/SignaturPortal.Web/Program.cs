using Microsoft.AspNetCore.SystemWebAdapters;
using SignaturPortal.Infrastructure;
using SignaturPortal.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Infrastructure layer - EF Core, repositories, Unit of Work
builder.Services.AddInfrastructure(builder.Configuration);

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

var app = builder.Build();

// Middleware pipeline - ORDER IS CRITICAL
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection(); // Only redirect to HTTPS in production
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseSystemWebAdapters(); // MUST be before UseAuthentication to set up remote auth/session
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
