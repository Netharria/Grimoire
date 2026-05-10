// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public static class ResultTaskExtensions
{
    extension<T>(Task<T> task)
    {
        public async Task<Result<T>> ToResult()
            => Result<T>.Ok(await task);
    }

    extension<T>(Task<Result<T>> task)
    {
        public async Task<Result<TOut>> Map<TOut>(Func<T, TOut> mapper)
            => (await task).Map(mapper);

        public async Task<Result<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> mapper)
            => await (await task).MapAsync(mapper);

        public async Task<Result<TOut>> Bind<TOut>(Func<T, Result<TOut>> binder)
            => (await task).Bind(binder);

        public async Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> binder)
            => await (await task).BindAsync(binder);

        public async Task<Result<T>> Tap(Action<T> action)
            => (await task).Tap(action);

        public async Task<Result<T>> TapAsync(Func<T, Task> action)
            => await (await task).TapAsync(action);

        public async Task<TOut> Match<TOut>(Func<T, TOut> onSuccess,
            Func<Error, TOut> onFail,
            Func<Error, TOut>? onNotFound = null,
            Func<Error, TOut>? onNotModified = null,
            Func<Error, TOut>? onConflict = null,
            Func<Error, TOut>? onForbidden = null)
            => (await task).Match(onSuccess, onFail, onNotFound, onNotModified, onConflict, onForbidden);

        public async Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess,
            Func<Error, TOut> onFail,
            Func<Error, TOut>? onNotFound = null,
            Func<Error, TOut>? onNotModified = null,
            Func<Error, TOut>? onConflict = null,
            Func<Error, TOut>? onForbidden = null)
            => await (await task).MatchAsync(onSuccess, onFail, onNotFound, onNotModified, onConflict, onForbidden);

        public async Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess,
            Func<Error, Task<TOut>> onFail,
            Func<Error, Task<TOut>>? onNotFound = null,
            Func<Error, Task<TOut>>? onNotModified = null,
            Func<Error, Task<TOut>>? onConflict = null,
            Func<Error, Task<TOut>>? onForbidden = null)
            => await (await task).MatchAsync(onSuccess, onFail, onNotFound, onNotModified, onConflict, onForbidden);

        public async Task<Result<T>> OrElse(Func<Result<T>> fallback)
            => (await task).OrElse(fallback);

        public async Task<T> GetOrElse(Func<T> fallback)
            => (await task).GetOrElse(fallback);
    }
}
