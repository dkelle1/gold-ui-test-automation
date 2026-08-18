using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Data;
using Respawn;
using Testcontainers.MsSql;
using Xunit;

namespace OrderService.IntegrationTests.Infrastructure;

/// <summary>Owns the throwaway SQL Server for the whole integration-test assembly.
///
/// It is expensive to start a SQL Server container and create the schema, so this is a *collection*
/// fixture: xUnit builds it once and shares it across every integration test (see
/// <see cref="IntegrationCollection"/>). What is cheap - a clean database - is what each test gets
/// instead, via <see cref="ResetAsync"/> using Respawn.</summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder().Build();
    private Respawner _respawner = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public CustomWebApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Create the schema once. A real service would apply EF migrations here; EnsureCreated keeps the
        // sample dependency-free (no migrations tooling) while still building the real provider's schema.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        Factory = new CustomWebApplicationFactory(ConnectionString);

        // Build the Respawner against the now-populated schema. WithReseed restarts identity columns
        // after each reset, so ids are deterministic across tests without hard-coding them.
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                SchemasToInclude = ["dbo"],
                WithReseed = true,
            });
    }

    /// <summary>Empties every table (keeping the schema) and reseeds identities - the per-test clean
    /// slate. Far faster than dropping and recreating the database or the container between tests.</summary>
    public async Task ResetAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}
