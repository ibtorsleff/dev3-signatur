using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SignaturPortal.Domain.Interfaces;
using SignaturPortal.Infrastructure.Data;
using SignaturPortal.Infrastructure.Repositories;

namespace SignaturPortal.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure layer services: EF Core DbContextFactory, repositories, UoW.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core with IDbContextFactory for Blazor Server circuit safety
        services.AddDbContextFactory<SignaturDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("SignaturAnnoncePortal"),
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(SignaturDbContext).Assembly.FullName)));

        // Unit of Work (creates its own DbContext via factory)
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
