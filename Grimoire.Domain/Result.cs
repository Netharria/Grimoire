// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace Grimoire.Domain;

public abstract record Result<T>
{
    public static Result<T> Ok(T value) => new Success(value);

    public static Result<T> Fail(Error error) => new Invalid([error]);

    public static Result<T> Fail(IEnumerable<Error> errors) => new Invalid([..errors]);

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

    public Result<T> Tap(Action<T> action)
        => this switch
        {
            Success(var v) => ExecuteTap(v, action),
            _ => this
        };

    private static Result<T> ExecuteTap(T item, Action<T> action)
    {
        try
        {
            action(item);
            return Ok(item);
        }
        catch (Exception)
        {
            return Fail(new Error("result.tap.fail", "Failed to execute action in pipeline."));
        }

    }

    public Task<Result<T>> TapAsync(Func<T, Task> action)
        => this switch
        {
            Success(var v) => ExecuteTapAsync(v, action),
            _ => Task.FromResult(this)
        };

    private static async Task<Result<T>> ExecuteTapAsync(T item, Func<T,Task> action)
    {
        try
        {
            await action(item);
            return Ok(item);
        }
        catch (Exception)
        {
            return Fail(new Error("result.tap.fail", "Failed to execute action in pipeline."));
        }

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
        Func<ImmutableArray<Error>, TOut> onFailure,
        Func<ImmutableArray<Error>, TOut>? onNotFound = null,
        Func<ImmutableArray<Error>, TOut>? onNotModified = null)
        => this switch
        {
            Success(var v) => onSuccess(v),
            Invalid(var e) => onFailure(e),
            NotFound(var e) => onNotFound is not null ? onNotFound([e]) : onFailure([e]),
            NotModified(var e) => onNotModified is not null ? onNotModified([e]) :  onFailure([e]),
            Conflict(var e) => onFailure([e]),
            Forbidden(var e) => onFailure([e]),
            _ => throw new UnreachableException()
        };

    public Task<TOut> MatchAsync<TOut>(
        Func<T, Task<TOut>> onSuccess,
        Func<ImmutableArray<Error>, TOut> onFailure,
        Func<ImmutableArray<Error>, TOut>? onNotFound = null,
        Func<ImmutableArray<Error>, TOut>? onNotModified = null)
        => this switch
        {
            Success(var v) => onSuccess(v),
            Invalid(var e) => Task.FromResult(onFailure(e)),
            NotFound(var e) => onNotFound is not null ? Task.FromResult(onNotFound([e]))  :  Task.FromResult(onFailure([e])),
            NotModified(var e) => onNotModified is not null ? Task.FromResult(onNotModified([e]))  : Task.FromResult(onFailure([e])),
            Conflict(var e) => Task.FromResult(onFailure([e])),
            Forbidden(var e) => Task.FromResult(onFailure([e])),
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
            Invalid(var errors) => Validation<T>.Fail(errors),
            NotFound(var e) => Validation<T>.Fail(e),
            NotModified(var e) => Validation<T>.Fail(e),
            Conflict(var e) => Validation<T>.Fail(e),
            Forbidden(var e) => Validation<T>.Fail(e),
            _ => throw new UnreachableException()
        };

    public sealed record Success(T Value) : Result<T>;

    public sealed record Invalid(ImmutableArray<Error> Errors) : Result<T>;

    public sealed record NotFound(Error Error) : Result<T>;

    public sealed record NotModified(Error Error) : Result<T>;

    public sealed record Conflict(Error Error) : Result<T>;

    public sealed record Forbidden(Error Error) : Result<T>;
}
