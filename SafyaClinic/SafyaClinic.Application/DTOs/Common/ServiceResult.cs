namespace SafyaClinic.Application.DTOs.Common;

public class ServiceResult
{
    public bool IsSuccess { get; protected set; }
    public string Message { get; protected set; } = string.Empty;
    public IEnumerable<string> Errors { get; protected set; } = Enumerable.Empty<string>();

    public static ServiceResult Success(string message = "")
        => new() { IsSuccess = true, Message = message };

    public static ServiceResult Failure(string error)
        => new() { IsSuccess = false, Errors = new[] { error } };

    public static ServiceResult Failure(IEnumerable<string> errors)
        => new() { IsSuccess = false, Errors = errors };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private set; }

    public static ServiceResult<T> Success(T data, string message = "")
        => new() { IsSuccess = true, Data = data, Message = message };

    public new static ServiceResult<T> Failure(string error)
        => new() { IsSuccess = false, Errors = new[] { error } };

    public new static ServiceResult<T> Failure(IEnumerable<string> errors)
        => new() { IsSuccess = false, Errors = errors };
}