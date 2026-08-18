using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderService.Api.Data;

namespace OrderService.IntegrationTests.Infrastructure;

/// <summary>Boots the real API in-process and repoints its <see cref="AppDbContext"/> at the
/// Testcontainers SQL Server. Everything else about the app - endpoints, DI, the service and repository
/// wiring - is exactly what production runs; only the connection string is swapped, which is what makes
/// these genuine full-stack integration tests rather than tests of a rewired app.</summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public CustomWebApplicationFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // ConfigureTestServices runs after the app's own registrations, so the app's DbContext (pointing
        // at the placeholder connection string) is already registered here and can be removed and
        // replaced with one bound to the container.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(_connectionString));
        });
    }
}
