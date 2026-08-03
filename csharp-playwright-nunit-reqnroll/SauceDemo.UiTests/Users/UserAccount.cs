namespace SauceDemo.UiTests.Users;

[Flags]
public enum UserCapabilities
{
    None = 0,
    CanLogIn = 1 << 0,
    CanCompleteCheckout = 1 << 1
}

public sealed record UserAccount(string Username, string Password, UserCapabilities Capabilities)
{
    public bool CanLogIn => Capabilities.HasFlag(UserCapabilities.CanLogIn);

    public bool CanCompleteCheckout => Capabilities.HasFlag(UserCapabilities.CanCompleteCheckout);

    public override string ToString() => Username;
}
