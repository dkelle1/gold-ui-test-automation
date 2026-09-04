using Xunit;

namespace OrderService.IntegrationTests.Infrastructure;

/// <summary>Binds every integration test class to the one shared <see cref="SqlServerContainerFixture"/>.
/// Placing them in a single collection also makes xUnit run them sequentially rather than in parallel -
/// which is what we want, since they all share one database and Respawn resets it between tests.</summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "integration";
}
