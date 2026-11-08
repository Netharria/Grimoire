// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace Grimoire.Domain;

public abstract record Validation<T>
{
    public static Validation<T> Succeed(T value) => new Valid(value);

    public static Validation<T> Fail(Error error) => new Invalid([error]);

    public static Validation<T> Fail(IEnumerable<Error> errors) => new Invalid([..errors]);

    public Validation<TOut> Map<TOut>(Func<T, TOut> mapper)
        => this switch
        {
            Valid(var v) => Validation<TOut>.Succeed(mapper(v)),
            Invalid(var e) => new Validation<TOut>.Invalid(e),
            _ => throw new UnreachableException()
        };

    public Validation<TOut> Bind<TOut>(Func<T, Validation<TOut>> binder)
        => this switch
        {
            Valid(var v) => binder(v),
            Invalid(var e) => new Validation<TOut>.Invalid(e),
            _ => throw new UnreachableException()
        };

    public TOut Match<TOut>(Func<T, TOut> onValid, Func<ImmutableArray<Error>, TOut> onInvalid)
        => this switch
        {
            Valid(var v) => onValid(v),
            Invalid(var e) => onInvalid(e),
            _ => throw new UnreachableException()
        };

    public T GetOrElse(Func<T> fallback)
        => this is Valid v ? v.Value : fallback();

    public Result<T> ToResult()
        => this switch
        {
            Valid(var v) => Result<T>.Ok(v),
            Invalid(var errors) => Result<T>.Fail(CombineErrors(errors)),
            _ => throw new UnreachableException()
        };

    private static Error CombineErrors(ImmutableArray<Error> errors)
    {
        var distinct = errors.Distinct().ToArray();
        return distinct.Length == 1
            ? distinct[0]
            : new Error("validation.failed", string.Join("; ", distinct.Select(e => e.Message)));
    }

    public sealed record Valid(T Value) : Validation<T>;

    public sealed record Invalid(ImmutableArray<Error> Errors) : Validation<T>;
}
