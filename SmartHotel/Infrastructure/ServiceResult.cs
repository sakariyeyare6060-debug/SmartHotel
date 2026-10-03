namespace SmartHotel.Infrastructure;

/// <summary>Outcome of a business operation; carries a user-friendly error on failure.</summary>
public class ServiceResult
{
    public bool Succeeded { get; protected init; }
    public string? Error { get; protected init; }

    public static ServiceResult Ok() => new() { Succeeded = true };
    public static ServiceResult Fail(string error) => new() { Succeeded = false, Error = error };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Value { get; private init; }

    public static ServiceResult<T> Ok(T value) => new() { Succeeded = true, Value = value };
    public static new ServiceResult<T> Fail(string error) => new() { Succeeded = false, Error = error };
}
