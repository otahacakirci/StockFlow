namespace StockFlow.Services.Common;

/// <summary>
/// Değer döndürmeyen Service işlemlerinin başarı veya beklenen hata sonucunu temsil eder.
/// </summary>
public sealed class ServiceResult
{
    private ServiceResult(bool isSuccess, ServiceError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public ServiceError? Error { get; }

    /// <summary>
    /// Hata içermeyen başarılı bir işlem sonucu oluşturur.
    /// </summary>
    public static ServiceResult Success()
    {
        return new ServiceResult(true, null);
    }

    /// <summary>
    /// Verilen beklenen hatayı taşıyan başarısız bir işlem sonucu oluşturur; hata boş olamaz.
    /// </summary>
    public static ServiceResult Failure(ServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ServiceResult(false, error);
    }
}

/// <summary>
/// Başarıda tipli değer, beklenen başarısızlıkta ise güvenli Service hatası taşıyan sonucu temsil eder.
/// </summary>
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

    /// <summary>
    /// Boş olmayan dönüş değerini taşıyan başarılı bir sonuç oluşturur.
    /// </summary>
    public static ServiceResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ServiceResult<T>(true, value, null);
    }

    /// <summary>
    /// Değer üretmeden verilen beklenen hatayı taşıyan başarısız bir sonuç oluşturur; hata boş olamaz.
    /// </summary>
    public static ServiceResult<T> Failure(ServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ServiceResult<T>(false, default, error);
    }
}
