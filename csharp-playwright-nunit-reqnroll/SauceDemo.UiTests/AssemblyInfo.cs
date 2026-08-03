using NUnit.Framework;
using SauceDemo.UiTests.Configuration;

// Scenario-level parallelism: Reqnroll generates one NUnit [Test] per scenario (and per Scenario
// Outline Examples row), so ParallelScope.Children runs those concurrently - including scenarios
// from the same feature file, which ParallelScope.Fixtures alone would not do.
//
// Per Reqnroll's parallel-execution guidance, this rules out [BeforeFeature]/[AfterFeature] hooks and
// FeatureContext/ScenarioContext.Current (neither is guaranteed under scenario-level parallelism) -
// this project has neither. See Hooks/ScenarioHooks.cs for how each scenario still gets an isolated
// browser + a distinct login user without any of that.
[assembly: Parallelizable(ParallelScope.Children)]

// Must match the size of Users.UserCatalog.PoolUsers - Users.UserPool asserts this at startup.
// LevelOfParallelismAttribute requires a compile-time constant, hence ParallelSettings.MaxParallelWorkers.
[assembly: LevelOfParallelism(ParallelSettings.MaxParallelWorkers)]
