// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;

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

        public async Task<TOut> Match<TOut>(Func<T, TOut> onSuccess,
            Func<ImmutableArray<Error>, TOut> onFail)
            => (await task).Match(onSuccess, onFail);

        public async Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess,
            Func<ImmutableArray<Error>, TOut> onFail)
            => await (await task).MatchAsync(onSuccess, onFail);

        public async Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess,
            Func<ImmutableArray<Error>, Task<TOut>> onFail)
        {
            var result = await task;
            return result switch
            {
                Result<T>.Success(var v) => await onSuccess(v),
                Result<T>.Invalid(var e) => await onFail(e),
                Result<T>.NotFound(var e) => await onFail([e]),
                Result<T>.NotModified(var e) => await onFail([e]),
                Result<T>.Conflict(var e) => await onFail([e]),
                Result<T>.Forbidden(var e) => await onFail([e]),
                _ => throw new UnreachableException()
            };
        }
    }
}
