// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Grimoire.Domain;

public static class ValidationTaskExtensions
{
    public static async Task<Validation<TOut>> Map<T, TOut>(
        this Task<Validation<T>> task,
        Func<T, TOut> mapper)
        => (await task).Map(mapper);

    public static async Task<Validation<TOut>> Bind<T, TOut>(
        this Task<Validation<T>> task,
        Func<T, Validation<TOut>> binder)
        => (await task).Bind(binder);

    public static async Task<Validation<TOut>> BindAsync<T, TOut>(
        this Task<Validation<T>> task,
        Func<T, Task<Validation<TOut>>> binder)
        => await (await task).BindAsync(binder);

    public static async Task<TOut> Match<T, TOut>(
        this Task<Validation<T>> task,
        Func<T, TOut> onValid,
        Func<ImmutableArray<Error>, TOut> onInvalid)
        => (await task).Match(onValid, onInvalid);

    public static async Task<TOut> MatchAsync<T, TOut>(
        this Task<Validation<T>> task,
        Func<T, Task<TOut>> onValid,
        Func<ImmutableArray<Error>, TOut> onInvalid)
        => await (await task).MatchAsync(onValid, onInvalid);

    public static async Task<TOut> MatchAsync<T, TOut>(
        this Task<Validation<T>> task,
        Func<T, Task<TOut>> onValid,
        Func<ImmutableArray<Error>, Task<TOut>> onInvalid)
    {
        var validation = await task;
        return validation switch
        {
            Validation<T>.Valid(var v) => await onValid(v),
            Validation<T>.Invalid(var e) => await onInvalid(e),
            _ => throw new System.Diagnostics.UnreachableException()
        };
    }
}
