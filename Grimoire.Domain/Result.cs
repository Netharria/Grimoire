// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Grimoire.Domain;

public abstract record Result<T>
{
    public static Result<T> Ok(T value) => new Success(value);

    public static Result<T> Fail(Error error) => new Invalid(error);

    public Result<TOut> Map<TOut>(Func<T, TOut> mapper)
        => this switch
        {
            Success(var v) => Result<TOut>.Ok(mapper(v)),
            Invalid(var e) => new Result<TOut>.Invalid(e),
            NotFound(var e) => new Result<TOut>.NotFound(e),
            NotModified(var e) => new Result<TOut>.NotModified(e),
            Conflict(var e) => new Result<TOut>.Conflict(e),
            Forbidden(var e) => new Result<TOut>.Forbidden(e),
            _ => throw new UnreachableException()
        };

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder)
        => this switch
        {
            Success(var v) => binder(v),
            Invalid(var e) => new Result<TOut>.Invalid(e),
            NotFound(var e) => new Result<TOut>.NotFound(e),
            NotModified(var e) => new Result<TOut>.NotModified(e),
            Conflict(var e) => new Result<TOut>.Conflict(e),
            Forbidden(var e) => new Result<TOut>.Forbidden(e),
            _ => throw new UnreachableException()
        };

    public Result<T> Tap(Action<T> action){
        if (this is Success(var v)) action(v);
        return this;
    }

    public async Task<Result<T>> TapAsync(Func<T, Task> action){
        if (this is Success(var v)) await action(v);
        return this;
    }

    public Task<Result<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> mapper)
        => BindAsync(async v => Result<TOut>.Ok(await mapper(v)));

    public Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> binder)
        => this switch
        {
            Success(var v) => binder(v),
            Invalid(var e) => Task.FromResult<Result<TOut>>(new Result<TOut>.Invalid(e)),
            NotFound(var e) => Task.FromResult<Result<TOut>>(new Result<TOut>.NotFound(e)),
            NotModified(var e) => Task.FromResult<Result<TOut>>(new Result<TOut>.NotModified(e)),
            Conflict(var e) => Task.FromResult<Result<TOut>>(new Result<TOut>.Conflict(e)),
            Forbidden(var e) => Task.FromResult<Result<TOut>>(new Result<TOut>.Forbidden(e)),
            _ => throw new UnreachableException()
        };

    public TOut Match<TOut>(
        Func<T, TOut> onSuccess,
        Func<Error, TOut> onFailure,
        Func<Error, TOut>? onNotFound = null,
        Func<Error, TOut>? onNotModified = null,
        Func<Error, TOut>? onConflict = null,
        Func<Error, TOut>? onForbidden = null)
        => this switch
        {
            Success(var v) => onSuccess(v),
            Invalid(var e) => onFailure(e),
            NotFound(var e) => onNotFound is not null ? onNotFound(e) : onFailure(e),
            NotModified(var e) => onNotModified is not null ? onNotModified(e) : onFailure(e),
            Conflict(var e) => onConflict is not null ? onConflict(e) : onFailure(e),
            Forbidden(var e) => onForbidden is not null ? onForbidden(e) : onFailure(e),
            _ => throw new UnreachableException()
        };

    public Task<TOut> MatchAsync<TOut>(
        Func<T, Task<TOut>> onSuccess,
        Func<Error, TOut> onFailure,
        Func<Error, TOut>? onNotFound = null,
        Func<Error, TOut>? onNotModified = null,
        Func<Error, TOut>? onConflict = null,
        Func<Error, TOut>? onForbidden = null)
        => this switch
        {
            Success(var v) => onSuccess(v),
            Invalid(var e) => Task.FromResult(onFailure(e)),
            NotFound(var e) => onNotFound is not null ? Task.FromResult(onNotFound(e)) : Task.FromResult(onFailure(e)),
            NotModified(var e) => onNotModified is not null ? Task.FromResult(onNotModified(e)) : Task.FromResult(onFailure(e)),
            Conflict(var e) => onConflict is not null ? Task.FromResult(onConflict(e)) : Task.FromResult(onFailure(e)),
            Forbidden(var e) => onForbidden is not null ? Task.FromResult(onForbidden(e)) : Task.FromResult(onFailure(e)),
            _ => throw new UnreachableException()
        };


    public Task<TOut> MatchAsync<TOut>(
        Func<T, Task<TOut>> onSuccess,
        Func<Error, Task<TOut>> onFailure,
        Func<Error, Task<TOut>>? onNotFound = null,
        Func<Error, Task<TOut>>? onNotModified = null,
        Func<Error, Task<TOut>>? onConflict = null,
        Func<Error, Task<TOut>>? onForbidden = null)
        => this switch
        {
            Success(var v) => onSuccess(v),
            Invalid(var e) => onFailure(e),
            NotFound(var e) => onNotFound is not null ? onNotFound(e) : onFailure(e),
            NotModified(var e) => onNotModified is not null ? onNotModified(e) : onFailure(e),
            Conflict(var e) => onConflict is not null ? onConflict(e) : onFailure(e),
            Forbidden(var e) => onForbidden is not null ? onForbidden(e) : onFailure(e),
            _ => throw new UnreachableException()
        };

    public Result<T> OrElse(Func<Result<T>> fallback)
        => this is Success ? this : fallback();

    public T GetOrElse(Func<T> fallback)
        => this is Success v ? v.Value : fallback();

    public Validation<T> ToValidation()
        => this switch
        {
            Success(var v) => Validation<T>.Succeed(v),
            Invalid(var e) => Validation<T>.Fail(e),
            NotFound(var e) => Validation<T>.Fail(e),
            NotModified(var e) => Validation<T>.Fail(e),
            Conflict(var e) => Validation<T>.Fail(e),
            Forbidden(var e) => Validation<T>.Fail(e),
            _ => throw new UnreachableException()
        };

    public sealed record Success(T Value) : Result<T>;

    public sealed record Invalid(Error Error) : Result<T>;

    public sealed record NotFound(Error Error) : Result<T>;

    public sealed record NotModified(Error Error) : Result<T>;

    public sealed record Conflict(Error Error) : Result<T>;

    public sealed record Forbidden(Error Error) : Result<T>;
}
