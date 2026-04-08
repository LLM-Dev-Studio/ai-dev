namespace AiDev.Models;

public abstract record Result<T>;

public sealed record Ok<T>(T Value) : Result<T>;

public sealed record Err<T>(DomainError Error) : Result<T>;

public sealed record DomainError(string Code, string Message);

public readonly record struct Unit
{
    public static readonly Unit Value = new();
}

public static class ResultExtensions
{
    public static async Task<Result<B>> Then<A, B>(
        this Task<Result<A>> resultTask,
        Func<A, Task<Result<B>>> next)
    {
        var result = await resultTask.ConfigureAwait(false);

        return result switch
        {
            Ok<A> ok => await next(ok.Value).ConfigureAwait(false),
            Err<A> err => new Err<B>(err.Error),
            _ => throw new UnreachableException(),
        };
    }

    public static Result<B> Then<A, B>(
        this Result<A> result,
        Func<A, Result<B>> next)
        => result switch
        {
            Ok<A> ok => next(ok.Value),
            Err<A> err => new Err<B>(err.Error),
            _ => throw new UnreachableException(),
        };

    public static Task<Result<B>> Then<A, B>(
        this Result<A> result,
        Func<A, Task<Result<B>>> next)
        => result switch
        {
            Ok<A> ok => next(ok.Value),
            Err<A> err => Task.FromResult<Result<B>>(new Err<B>(err.Error)),
            _ => throw new UnreachableException(),
        };

    public static T Match<TValue, T>(
        this Result<TValue> result,
        Func<TValue, T> onOk,
        Func<DomainError, T> onErr)
        => result switch
        {
            Ok<TValue> ok => onOk(ok.Value),
            Err<TValue> err => onErr(err.Error),
            _ => throw new UnreachableException(),
        };

    public static string? ToErrorMessage<T>(this Result<T> result)
        => result.Match(_ => (string?)null, err => err.Message);
}
