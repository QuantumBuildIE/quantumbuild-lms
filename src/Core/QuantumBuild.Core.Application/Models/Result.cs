namespace QuantumBuild.Core.Application.Models;

public class Result
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();

    public static Result Ok()
    {
        return new Result { Success = true };
    }

    public static Result<T> Ok<T>(T data)
    {
        return new Result<T> { Success = true, Data = data };
    }

    public static Result Fail(string error)
    {
        return new Result
        {
            Success = false,
            Errors = new List<string> { error }
        };
    }

    public static Result Fail(List<string> errors)
    {
        return new Result
        {
            Success = false,
            Errors = errors
        };
    }

    public static Result<T> Fail<T>(string error)
    {
        return new Result<T>
        {
            Success = false,
            Errors = new List<string> { error }
        };
    }

    public static Result<T> Fail<T>(List<string> errors)
    {
        return new Result<T>
        {
            Success = false,
            Errors = errors
        };
    }
}

public class Result<T> : Result
{
    public T? Data { get; set; }
}
