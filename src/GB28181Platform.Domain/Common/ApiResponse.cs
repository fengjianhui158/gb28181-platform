namespace GB28181Platform.Domain.Common;

public class ApiResponse<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = "success";
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data) => new() { Code = 0, Data = data };
    public static ApiResponse<T> Fail(string message, int code = -1) => new() { Code = code, Message = message };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok() => new() { Code = 0 };
    public new static ApiResponse Fail(string message, int code = -1) => new() { Code = code, Message = message };
}
