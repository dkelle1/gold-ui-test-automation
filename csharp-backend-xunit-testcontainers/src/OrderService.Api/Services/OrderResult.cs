using OrderService.Api.Domain;

namespace OrderService.Api.Services;

/// <summary>Why an order could not be placed. Every value is a client error (the request asked for
/// something invalid), which is why the API maps them all to 400 rather than 500.</summary>
public enum OrderError
{
    EmptyOrder,
    InvalidQuantity,
    UnknownProduct,
    InsufficientStock,
}

/// <summary>Outcome of a placement attempt: either the created <see cref="Order"/> or a typed error with
/// a human-readable message. A result type (rather than throwing) keeps the service's failure modes
/// explicit and makes the unit tests assert on data instead of catching exceptions.</summary>
public sealed class OrderResult
{
    private OrderResult(bool isSuccess, Order? order, OrderError? error, string? message)
    {
        IsSuccess = isSuccess;
        Order = order;
        Error = error;
        Message = message;
    }

    public bool IsSuccess { get; }
    public Order? Order { get; }
    public OrderError? Error { get; }
    public string? Message { get; }

    public static OrderResult Success(Order order) => new(true, order, null, null);

    public static OrderResult Fail(OrderError error, string message) => new(false, null, error, message);
}
