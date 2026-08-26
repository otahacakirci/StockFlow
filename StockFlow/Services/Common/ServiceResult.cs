namespace StockFlow.Services.Common;

public sealed class ServiceResult
{
    private ServiceResult(bool isSuccess, ServiceError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public ServiceError? Error { get; }

    public static ServiceResult Success()
    {
        return new ServiceResult(true, null);
    }

    public static ServiceResult Failure(ServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ServiceResult(false, error);
    }
}

public sealed class ServiceResult<T>
{
    private ServiceResult(bool isSuccess, T? value, ServiceError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public T? Value { get; }

    public ServiceError? Error { get; }

    public static ServiceResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ServiceResult<T>(true, value, null);
    }

    public static ServiceResult<T> Failure(ServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ServiceResult<T>(false, default, error);
    }
}
