using Xunit;

namespace OrderService.IntegrationTests.Infrastructure;

/// <summary>Common per-test setup: reset the database to empty (Respawn) and hand the test a fresh
/// <see cref="HttpClient"/> against the shared app. Because the reset runs in
/// <see cref="InitializeAsync"/> - which xUnit calls before every test - each test starts from a known
/// clean state and can assume identity ids start at 1.</summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly SqlServerContainerFixture _fixture;

    protected IntegrationTestBase(SqlServerContainerFixture fixture) => _fixture = fixture;

    protected HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        Client = _fixture.Factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        Client?.Dispose();
        return Task.CompletedTask;
    }
}
