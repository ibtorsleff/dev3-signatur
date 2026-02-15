using SignaturPortal.Application.Interfaces;
using SignaturPortal.Web.Services;

namespace SignaturPortal.Web.Middleware;

public class UserSessionMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var sessionContext = context.RequestServices.GetService<IUserSessionContext>();
        if (sessionContext is UserSessionContext impl)
        {
            impl.Initialize();
        }

        return next(context);
    }
}
