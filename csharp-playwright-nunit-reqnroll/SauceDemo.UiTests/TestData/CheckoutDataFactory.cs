using Bogus;

namespace SauceDemo.UiTests.TestData;

/// <summary>
/// Generates realistic checkout form data with Bogus. Login credentials are never generated this
/// way - see <see cref="Users.UserCatalog"/> - only data the app accepts freely.
///
/// Builds a fresh <see cref="Faker{T}"/> on every call rather than caching one in a static field:
/// Faker&lt;T&gt; instances are not thread-safe, and scenarios run concurrently across NUnit workers, so a
/// shared instance would be a source of rare, hard-to-reproduce flakes.
/// </summary>
public static class CheckoutDataFactory
{
    public static CheckoutInfo Create(int? seed = null)
    {
        var faker = new Faker<CheckoutInfo>()
            .CustomInstantiator(f => new CheckoutInfo(
                f.Name.FirstName(),
                f.Name.LastName(),
                f.Address.ZipCode("#####")));

        if (seed.HasValue)
        {
            // Bogus.Randomizer.Seed is a global static and NOT thread-safe - never set it directly.
            // Faker<T>.UseSeed is instance-level and safe to use here for reproducible scenarios.
            faker = faker.UseSeed(seed.Value);
        }

        return faker.Generate();
    }
}
