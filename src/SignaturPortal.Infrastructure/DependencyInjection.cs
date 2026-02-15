using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Domain.Interfaces;
using SignaturPortal.Infrastructure.Authorization;
using SignaturPortal.Infrastructure.Data;
using SignaturPortal.Infrastructure.Interceptors;
using SignaturPortal.Infrastructure.Repositories;
using SignaturPortal.Infrastructure.Services;

namespace SignaturPortal.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure layer services: EF Core DbContextFactory, repositories, UoW,
    /// tenant interceptor, permission service, and authorization handler.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Tenant write guard interceptor (singleton — stateless)
        services.AddSingleton<TenantSaveChangesInterceptor>();

        // EF Core with IDbContextFactory for Blazor Server circuit safety
        services.AddDbContextFactory<SignaturDbContext>((sp, options) =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("SignaturAnnoncePortal"),
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(SignaturDbContext).Assembly.FullName));
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        // Unit of Work (creates its own DbContext via factory, stamps tenant context)
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Permission service (read-only, scoped — caches per-request)
        services.AddScoped<IPermissionService, PermissionService>();

        // Authorization handler for permission-based policies
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();

        return services;
    }
}
