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
            await impl.InitializeAsync();
        }

        await next(context);
    }
}
