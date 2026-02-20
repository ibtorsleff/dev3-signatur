using SignaturPortal.Application.Interfaces;
using SignaturPortal.Web.Services;

namespace SignaturPortal.Web.Middleware;

public class UserSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sessionContext = context.RequestServices.GetService<IUserSessionContext>();
        if (sessionContext is UserSessionContext impl)
        {
            // Read ImpersonatedBy from System.Web session (available during SSR after UseSystemWebAdapters).
            // Holds the original admin's UserId when an admin is impersonating another user.
            var rawImpersonatedBy = System.Web.HttpContext.Current?.Session?["ImpersonatedBy"];
            Guid? impersonatedByUserId = rawImpersonatedBy is Guid g ? g : null;

            await impl.InitializeAsync(context.User.Identity?.Name, impersonatedByUserId);
        }

        await next(context);
    }
}
