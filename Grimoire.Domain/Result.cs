// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


namespace Grimoire.Domain;

public abstract record Result<T>
{
    public static Result<T> Ok(T value) => new Success(value);

    public static Result<T> Fail(Error error) => new Invalid(error);

    public Result<TOut> Map<TOut>(Func<T, TOut> mapper)
        => this is Success(var v) ? Result<TOut>.Ok(mapper(v)) : this.CarryFailure<TOut>();

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder)
        => this is Success(var v) ? binder(v) : this.CarryFailure<TOut>();

    public async Task<Result<T>> TapAsync(Func<T, Task> action)
    {
        if (this is Success(var v)) await action(v);
        return this;
    }

    public Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> binder)
        => this is Success(var v) ? binder(v) : Task.FromResult(this.CarryFailure<TOut>());

    /// <summary>
    ///     <paramref name="onNotFound" /> and <paramref name="onNotModified" /> are optional: a case without its own
    ///     handler goes to <paramref name="onFailure" />.
    /// </summary>
    public TOut Match<TOut>(
        Func<T, TOut> onSuccess,
        Func<Error, TOut> onFailure,
        Func<Error, TOut>? onNotFound = null,
        Func<Error, TOut>? onNotModified = null)
        => this switch
        {
            Success(var v) => onSuccess(v),
            NotFound(var e) when onNotFound is not null => onNotFound(e),
            NotModified(var e) when onNotModified is not null => onNotModified(e),
            _ => onFailure(this.FailureError!)
        };

    public Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<Error, TOut> onFailure)
        => this is Success(var v) ? onSuccess(v) : Task.FromResult(onFailure(this.FailureError!));

    public Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure)
        => this is Success(var v) ? onSuccess(v) : onFailure(this.FailureError!);

    /// <summary>Runs a side-effecting async action on success or failure. Returns a plain <c>Task</c> so both branches can be <c>async Task</c> without forcing a generic return type.</summary>
    public Task MatchAsync(Func<T, Task> onSuccess, Func<Error, Task> onFailure)
        => this is Success(var v) ? onSuccess(v) : onFailure(this.FailureError!);

    public T GetOrElse(Func<T> fallback)
        => this is Success v ? v.Value : fallback();

    /// <summary>The error of a failed result, or <c>null</c> on success.</summary>
    internal Error? FailureError => this switch
    {
        Success => null,
        Invalid(var e) => e,
        NotFound(var e) => e,
        NotModified(var e) => e,
        _ => throw new InvariantViolationException()
    };

    /// <summary>The same failure case and error as a result of another type. Only called on a failure.</summary>
    private Result<TOut> CarryFailure<TOut>() => this switch
    {
        Invalid(var e) => new Result<TOut>.Invalid(e),
        NotFound(var e) => new Result<TOut>.NotFound(e),
        NotModified(var e) => new Result<TOut>.NotModified(e),
        _ => throw new InvariantViolationException()
    };

    public sealed record Success(T Value) : Result<T>;

    public sealed record Invalid(Error Error) : Result<T>;

    public sealed record NotFound(Error Error) : Result<T>;

    public sealed record NotModified(Error Error) : Result<T>;
}
