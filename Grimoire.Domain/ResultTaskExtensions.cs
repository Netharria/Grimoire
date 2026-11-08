// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public static class ResultTaskExtensions
{
    extension<T>(Task<Result<T>> task)
    {
        public async Task<Result<TOut>> Map<TOut>(Func<T, TOut> mapper)
            => (await task).Map(mapper);

        public async Task<Result<TOut>> Bind<TOut>(Func<T, Result<TOut>> binder)
            => (await task).Bind(binder);

        public async Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> binder)
            => await (await task).BindAsync(binder);

        public async Task<Result<T>> TapAsync(Func<T, Task> action)
            => await (await task).TapAsync(action);

        public async Task<TOut> Match<TOut>(Func<T, TOut> onSuccess,
            Func<Error, TOut> onFail,
            Func<Error, TOut>? onNotFound = null,
            Func<Error, TOut>? onNotModified = null)
            => (await task).Match(onSuccess, onFail, onNotFound, onNotModified);

        /// <summary>
        ///     For handlers that return a <c>Task</c>: awaits the handler too. Without this overload the generic one
        ///     returns a <c>Task&lt;Task&gt;</c>, and <c>await</c> would finish before the handler does.
        /// </summary>
        public async Task Match(Func<T, Task> onSuccess,
            Func<Error, Task> onFail,
            Func<Error, Task>? onNotFound = null,
            Func<Error, Task>? onNotModified = null)
            => await (await task).Match(onSuccess, onFail, onNotFound, onNotModified);

        /// <summary>The same as the <c>Task</c> overload, for handlers that return a <c>ValueTask</c>.</summary>
        public async Task Match(Func<T, ValueTask> onSuccess,
            Func<Error, ValueTask> onFail,
            Func<Error, ValueTask>? onNotFound = null,
            Func<Error, ValueTask>? onNotModified = null)
            => await (await task).Match(onSuccess, onFail, onNotFound, onNotModified);

        public async Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<Error, TOut> onFail)
            => await (await task).MatchAsync(onSuccess, onFail);

        public async Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFail)
            => await (await task).MatchAsync(onSuccess, onFail);

        /// <summary>Runs a side-effecting async action on success or failure. Returns a plain <c>Task</c> so both branches can be <c>async Task</c> without forcing a generic return type.</summary>
        public async Task MatchAsync(Func<T, Task> onSuccess, Func<Error, Task> onFail)
            => await (await task).MatchAsync(onSuccess, onFail);

        public async Task<T> GetOrElse(Func<T> fallback)
            => (await task).GetOrElse(fallback);
    }
}
