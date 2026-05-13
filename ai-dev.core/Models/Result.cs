namespace AiDev.Models;

/// <summary>
/// Represents the result of an operation that can either succeed or fail.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
public abstract record Result<T>;

/// <summary>
/// Represents a successful result.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
/// <param name="Value">The successful value.</param>
public sealed record Ok<T>(T Value) : Result<T>;

/// <summary>
/// Represents a failed result.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
/// <param name="Error">The failure details.</param>
public sealed record Err<T>(DomainError Error) : Result<T>;

/// <summary>
/// Represents a domain-level error.
/// </summary>
/// <param name="Code">The machine-readable error code.</param>
/// <param name="Message">The human-readable error message.</param>
public sealed record DomainError(string Code, string Message);

/// <summary>
/// Represents the absence of a meaningful value.
/// </summary>
public readonly record struct Unit
{
    /// <summary>
    /// Gets the singleton unit value.
    /// </summary>
    public static readonly Unit Value = new();
}

/// <summary>
/// Provides helpers for composing and projecting <see cref="Result{T}"/> values.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Chains an asynchronous result into another asynchronous result.
    /// </summary>
    /// <typeparam name="A">The input success type.</typeparam>
    /// <typeparam name="B">The output success type.</typeparam>
    /// <param name="resultTask">The source asynchronous result.</param>
    /// <param name="next">The continuation to invoke when the source succeeds.</param>
    /// <returns>The composed asynchronous result.</returns>
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

    /// <summary>
    /// Chains a synchronous result into another synchronous result.
    /// </summary>
    /// <typeparam name="A">The input success type.</typeparam>
    /// <typeparam name="B">The output success type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="next">The continuation to invoke when the source succeeds.</param>
    /// <returns>The composed result.</returns>
    public static Result<B> Then<A, B>(
        this Result<A> result,
        Func<A, Result<B>> next)
        => result switch
        {
            Ok<A> ok => next(ok.Value),
            Err<A> err => new Err<B>(err.Error),
            _ => throw new UnreachableException(),
        };

    /// <summary>
    /// Chains a synchronous result into an asynchronous result.
    /// </summary>
    /// <typeparam name="A">The input success type.</typeparam>
    /// <typeparam name="B">The output success type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="next">The asynchronous continuation to invoke when the source succeeds.</param>
    /// <returns>The composed asynchronous result.</returns>
    public static Task<Result<B>> Then<A, B>(
        this Result<A> result,
        Func<A, Task<Result<B>>> next)
        => result switch
        {
            Ok<A> ok => next(ok.Value),
            Err<A> err => Task.FromResult<Result<B>>(new Err<B>(err.Error)),
            _ => throw new UnreachableException(),
        };

    /// <summary>
    /// Projects a result into a single output value.
    /// </summary>
    /// <typeparam name="TValue">The input success type.</typeparam>
    /// <typeparam name="T">The projected output type.</typeparam>
    /// <param name="result">The result to project.</param>
    /// <param name="onOk">The projection for a successful result.</param>
    /// <param name="onErr">The projection for a failed result.</param>
    /// <returns>The projected value.</returns>
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

    /// <summary>
    /// Extracts the error message from a failed result.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <returns>The error message for a failed result; otherwise, <see langword="null"/>.</returns>
    public static string? ToErrorMessage<T>(this Result<T> result)
        => result.Match(_ => (string?)null, err => err.Message);
}
