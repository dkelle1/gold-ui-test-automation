namespace SauceDemo.UiTests.Configuration;

/// <summary>
/// Single source of truth for how many scenarios run concurrently. Must stay in sync with
/// <see cref="Users.UserCatalog.PoolUsers"/> (one distinct login user per parallel worker) and is
/// consumed by the compile-time <c>LevelOfParallelismAttribute</c> in AssemblyInfo.cs, which requires
/// a constant.
/// </summary>
public static class ParallelSettings
{
    public const int MaxParallelWorkers = 3;
}
