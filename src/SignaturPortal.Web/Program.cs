using Microsoft.AspNetCore.SystemWebAdapters;
using SignaturPortal.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// System.Web Adapters - Session + Auth sharing with legacy WebForms
builder.Services.AddSystemWebAdapters()
    .AddJsonSessionSerializer(options =>
    {
        // Register session keys used by legacy WebForms app.
        // IMPORTANT: These must match exactly what the WebForms app registers.
        // Based on audit of legacy codebase: Session["SiteId"], Session["ClientId"], Session["UserId"], Session["UserName"], Session["UserLanguageId"]
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

// HttpContext accessor for session access in services
builder.Services.AddHttpContextAccessor();

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
app.UseAuthentication();
app.UseAuthorization();
app.UseSystemWebAdapters();
app.UseAntiforgery();

// Blazor components (higher precedence - matched first)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// YARP fallback to WebForms (MUST be last - lowest precedence)
// int.MaxValue ensures this only matches when no Blazor route handles the request
var remoteAppUrl = builder.Configuration["RemoteAppUri"]!;
app.MapForwarder("/{**catch-all}", remoteAppUrl)
    .WithOrder(int.MaxValue)
    .ShortCircuit();

app.Run();
