namespace OrderService.Api.Services;

/// <summary>The current time, behind an interface so order timestamps are deterministic in unit tests
/// (substitute a fixed clock) instead of reading the wall clock directly.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
