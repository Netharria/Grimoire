// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain.Migration;

/// <summary>
///     Bridge for the language-ext migration (spec 1.1, 3.4): the chain methods that <c>Task&lt;Result&lt;T&gt;&gt;</c>
///     had, on <c>Task&lt;Fin&lt;T&gt;&gt;</c> and <c>Fin&lt;T&gt;</c>, so the callers of the <c>Task</c>-based
///     settings services change only their types. Every member is obsolete, so each use is a build warning to burn
///     down. The Phase 3 ports replace them with <c>Eff</c> pipelines, and Phase 5 deletes this class.
/// </summary>
public static class FinTaskExtensions
{
    private const string Bridge = "Bridge for the language-ext migration; replace with an Eff pipeline (spec 1.1).";

    private const string DefaultBridge =
        "Hides a failure behind a default (TD-13). Replace when the call site is ported and its boundary handles the "
        + "failure (spec 1.1, OQ-1).";

    extension<T>(Fin<T> fin)
    {
        [Obsolete(Bridge)]
        public async Task<Fin<T>> TapAsync(Func<T, Task> action)
        {
            if (fin is Fin<T>.Succ(var value))
                await action(value);
            return fin;
        }

        [Obsolete(Bridge)]
        public Task<Fin<TOut>> BindAsync<TOut>(Func<T, Task<Fin<TOut>>> binder)
            => fin.Match(binder, error => Task.FromResult(Fin.Fail<TOut>(error)));

        [Obsolete(Bridge)]
        public Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<Error, TOut> onFail)
            => fin.Match(onSuccess, error => Task.FromResult(onFail(error)));

        [Obsolete(Bridge)]
        public Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFail)
            => fin.Match(onSuccess, onFail);

        /// <summary>Runs a side-effecting async action on success or failure.</summary>
        [Obsolete(Bridge)]
        public Task MatchAsync(Func<T, Task> onSuccess, Func<Error, Task> onFail)
            => fin.Match(onSuccess, onFail);

        /// <summary>The value, or <paramref name="fallback" /> on any failure. Replaces <c>GetOrElse</c>.</summary>
        [Obsolete(DefaultBridge)]
        public T OrDefaultDuringMigration(T fallback)
            => fin.Match(value => value, _ => fallback);
    }

    extension<T>(Fin<Option<T>> fin) where T : struct
    {
        /// <summary>
        ///     The value, or <c>null</c> when there is none or on any failure. Replaces <c>GetOrElse(() =&gt; null)</c>
        ///     for code that still uses <c>null</c> for an absent id.
        /// </summary>
        [Obsolete(DefaultBridge)]
        public T? OrNullDuringMigration()
            => fin.Match(option => option.ToNullable(), _ => null);
    }

    extension<T>(Task<Fin<Option<T>>> task) where T : struct
    {
        /// <inheritdoc cref="FinTaskExtensions.OrNullDuringMigration{T}(Fin{Option{T}})" />
        [Obsolete(DefaultBridge)]
        public async Task<T?> OrNullDuringMigration()
            => (await task).OrNullDuringMigration();
    }

    extension<T>(Task<Fin<T>> task)
    {
        [Obsolete(Bridge)]
        public async Task<Fin<TOut>> MapFin<TOut>(Func<T, TOut> mapper)
            => (await task).Map(mapper);

        [Obsolete(Bridge)]
        public async Task<Fin<TOut>> BindFin<TOut>(Func<T, Fin<TOut>> binder)
            => (await task).Bind(binder);

        [Obsolete(Bridge)]
        public async Task<Fin<TOut>> BindAsync<TOut>(Func<T, Task<Fin<TOut>>> binder)
            => await (await task).BindAsync(binder);

        [Obsolete(Bridge)]
        public async Task<Fin<T>> TapAsync(Func<T, Task> action)
            => await (await task).TapAsync(action);

        [Obsolete(Bridge)]
        public async Task<TOut> Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFail)
            => (await task).Match(onSuccess, onFail);

        /// <summary>
        ///     For handlers that return a <c>Task</c>: awaits the handler too. Without this overload the generic one
        ///     returns a <c>Task&lt;Task&gt;</c>, and <c>await</c> would finish before the handler does.
        /// </summary>
        [Obsolete(Bridge)]
        public async Task Match(Func<T, Task> onSuccess, Func<Error, Task> onFail)
            => await (await task).Match(onSuccess, onFail);

        /// <summary>The same as the <c>Task</c> overload, for handlers that return a <c>ValueTask</c>.</summary>
        [Obsolete(Bridge)]
        public async Task Match(Func<T, ValueTask> onSuccess, Func<Error, ValueTask> onFail)
            => await (await task).Match(onSuccess, onFail);

        [Obsolete(Bridge)]
        public async Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<Error, TOut> onFail)
            => await (await task).MatchAsync(onSuccess, onFail);

        [Obsolete(Bridge)]
        public async Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFail)
            => await (await task).MatchAsync(onSuccess, onFail);

        /// <summary>Runs a side-effecting async action on success or failure.</summary>
        [Obsolete(Bridge)]
        public async Task MatchAsync(Func<T, Task> onSuccess, Func<Error, Task> onFail)
            => await (await task).MatchAsync(onSuccess, onFail);

        /// <inheritdoc cref="FinTaskExtensions.OrDefaultDuringMigration{T}(Fin{T}, T)" />
        [Obsolete(DefaultBridge)]
        public async Task<T> OrDefaultDuringMigration(T fallback)
            => (await task).OrDefaultDuringMigration(fallback);
    }
}
