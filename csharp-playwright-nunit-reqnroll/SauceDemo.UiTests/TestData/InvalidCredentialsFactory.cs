using Bogus;
using SauceDemo.UiTests.Users;

namespace SauceDemo.UiTests.TestData;

/// <summary>Generates random credentials for negative login scenarios, guaranteed not to collide with a real UserCatalog account.</summary>
public static class InvalidCredentialsFactory
{
    public static (string Username, string Password) Create()
    {
        var faker = new Faker();

        string username;
        do
        {
            username = faker.Internet.UserName();
        } while (UserCatalog.AllUsers.ContainsKey(username));

        var password = faker.Internet.Password(length: 12);
        return (username, password);
    }
}
