// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public static class Result
{
    /// <summary>
    ///     Awaits six results together. Succeeds with all six values, or fails as <c>Invalid</c> with every distinct error.
    /// </summary>
    public static async Task<Result<(T1, T2, T3, T4, T5, T6)>> WhenAll<T1, T2, T3, T4, T5, T6>(
        Task<Result<T1>> t1,
        Task<Result<T2>> t2,
        Task<Result<T3>> t3,
        Task<Result<T4>> t4,
        Task<Result<T5>> t5,
        Task<Result<T6>> t6)
    {
        await Task.WhenAll(t1, t2, t3, t4, t5, t6);
        if ((t1.Result, t2.Result, t3.Result, t4.Result, t5.Result, t6.Result) is (
            Result<T1>.Success(var a), Result<T2>.Success(var b), Result<T3>.Success(var c),
            Result<T4>.Success(var d), Result<T5>.Success(var e), Result<T6>.Success(var f)))
            return Result<(T1, T2, T3, T4, T5, T6)>.Ok((a, b, c, d, e, f));

        Error[] errors =
        [
            .. new[]
                {
                    t1.Result.FailureError, t2.Result.FailureError, t3.Result.FailureError,
                    t4.Result.FailureError, t5.Result.FailureError, t6.Result.FailureError
                }
                .OfType<Error>()
                .Distinct()
        ];
        return Result<(T1, T2, T3, T4, T5, T6)>.Fail(errors.Length == 1
            ? errors[0]
            : new Error("validation.failed", string.Join("; ", errors.Select(error => error.Message))));
    }
}
