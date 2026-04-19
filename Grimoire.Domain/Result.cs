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
    public sealed record Success(T Value) : Result<T>;

    public sealed record Invalid(ImmutableArray<Error> Errors) : Result<T>;

    public sealed record NotFound(Error Error) : Result<T>;

    public sealed record NotModified(Error Error) : Result<T>;

    public sealed record Conflict(Error Error) : Result<T>;

    public sealed record Forbidden(Error Error) : Result<T>;

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

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Result<T>, TOut> onFailure)
        => this is Success(var v) ? onSuccess(v) : onFailure(this);

    public Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<Result<T>, TOut> onFailure)
        => this is Success(var v) ? onSuccess(v) : Task.FromResult(onFailure(this));

    public Result<T> OrElse(Func<Result<T>> fallback)
        => this is Success ? this : fallback();

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
}
