// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace Grimoire.Domain;

public static class ValidationTaskExtensions
{
    extension<T>(Task<T> task)
    {
        public async Task<Validation<T>> ToValidation()
            => Validation<T>.Succeed(await task);
    }

    extension<T>(Task<Validation<T>> task)
    {
        public async Task<Result<T>> ToResult()
            => (await task).ToResult();

        public async Task<Validation<TOut>> Map<TOut>(Func<T, TOut> mapper)
            => (await task).Map(mapper);

        public async Task<Validation<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> mapper)
            => await (await task).MapAsync(mapper);

        public async Task<Validation<TOut>> Bind<TOut>(Func<T, Validation<TOut>> binder)
            => (await task).Bind(binder);

        public async Task<Validation<TOut>> BindAsync<TOut>(Func<T, Task<Validation<TOut>>> binder)
            => await (await task).BindAsync(binder);

        public async Task<TOut> Match<TOut>(Func<T, TOut> onValid,
            Func<ImmutableArray<Error>, TOut> onInvalid)
            => (await task).Match(onValid, onInvalid);

        public async Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onValid,
            Func<ImmutableArray<Error>, TOut> onInvalid)
            => await (await task).MatchAsync(onValid, onInvalid);

        public async Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onValid,
            Func<ImmutableArray<Error>, Task<TOut>> onInvalid)
        {
            var validation = await task;
            return validation switch
            {
                Validation<T>.Valid(var v) => await onValid(v),
                Validation<T>.Invalid(var e) => await onInvalid(e),
                _ => throw new UnreachableException()
            };
        }

        public async Task<Validation<T>> OrElse(Func<Validation<T>> fallback)
            => (await task).OrElse(fallback);
        public async Task<T> GetOrElse(Func<T> fallback)
            => (await task).GetOrElse(fallback);
    }
}
