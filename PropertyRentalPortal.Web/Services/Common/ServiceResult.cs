namespace PropertyRentalPortal.Web.Services.Common;

public class ServiceResult
{
    public bool Success { get; protected set; }
    public string? ErrorMessage { get; protected set; }
    public IDictionary<string, string[]> ValidationErrors { get; protected set; } = new Dictionary<string, string[]>();

    public static ServiceResult Ok() => new() { Success = true };

    public static ServiceResult Fail(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };

    public static ServiceResult Invalid(IDictionary<string, string[]> errors, string? message = null) => new()
    {
        Success = false,
        ErrorMessage = message ?? "Validation failed.",
        ValidationErrors = errors
    };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private set; }

    public static ServiceResult<T> Ok(T data) => new()
    {
        Success = true,
        Data = data
    };

    public new static ServiceResult<T> Fail(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };

    public new static ServiceResult<T> Invalid(IDictionary<string, string[]> errors, string? message = null) => new()
    {
        Success = false,
        ErrorMessage = message ?? "Validation failed.",
        ValidationErrors = errors
    };
}
