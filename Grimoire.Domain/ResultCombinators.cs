// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Grimoire.Domain;

public static class Result
{
    public static async Task<Result<(T1, T2)>> WhenAll<T1, T2>(
        Task<Result<T1>> t1,
        Task<Result<T2>> t2)
    {
        await Task.WhenAll(t1, t2);
        return Validation.Combine(t1.Result.ToValidation(), t2.Result.ToValidation()).ToResult();
    }

    public static async Task<Result<(T1, T2, T3)>> WhenAll<T1, T2, T3>(
        Task<Result<T1>> t1,
        Task<Result<T2>> t2,
        Task<Result<T3>> t3)
    {
        await Task.WhenAll(t1, t2, t3);
        return Validation.Combine(t1.Result.ToValidation(), t2.Result.ToValidation(), t3.Result.ToValidation()).ToResult();
    }

    public static async Task<Result<(T1, T2, T3, T4)>> WhenAll<T1, T2, T3, T4>(
        Task<Result<T1>> t1,
        Task<Result<T2>> t2,
        Task<Result<T3>> t3,
        Task<Result<T4>> t4)
    {
        await Task.WhenAll(t1, t2, t3, t4);
        return Validation.Combine(
            t1.Result.ToValidation(), t2.Result.ToValidation(),
            t3.Result.ToValidation(), t4.Result.ToValidation()).ToResult();
    }

    public static async Task<Result<(T1, T2, T3, T4, T5)>> WhenAll<T1, T2, T3, T4, T5>(
        Task<Result<T1>> t1,
        Task<Result<T2>> t2,
        Task<Result<T3>> t3,
        Task<Result<T4>> t4,
        Task<Result<T5>> t5)
    {
        await Task.WhenAll(t1, t2, t3, t4, t5);
        return Validation.Combine(
            t1.Result.ToValidation(), t2.Result.ToValidation(), t3.Result.ToValidation(),
            t4.Result.ToValidation(), t5.Result.ToValidation()).ToResult();
    }

    public static async Task<Result<(T1, T2, T3, T4, T5, T6)>> WhenAll<T1, T2, T3, T4, T5, T6>(
        Task<Result<T1>> t1,
        Task<Result<T2>> t2,
        Task<Result<T3>> t3,
        Task<Result<T4>> t4,
        Task<Result<T5>> t5,
        Task<Result<T6>> t6)
    {
        await Task.WhenAll(t1, t2, t3, t4, t5, t6);
        return Validation.Combine(
            t1.Result.ToValidation(), t2.Result.ToValidation(), t3.Result.ToValidation(),
            t4.Result.ToValidation(), t5.Result.ToValidation(), t6.Result.ToValidation()).ToResult();
    }

    public static async Task<Result<IReadOnlyList<T>>> WhenAll<T>(IEnumerable<Task<Result<T>>> tasks)
    {
        var results = await Task.WhenAll(tasks);
        var values = new List<T>(results.Length);
        var errors = new List<Error>();
        foreach (var result in results)
        {
            if (result is Result<T>.Success(var v))
                values.Add(v);
            else
                errors.Add(ExtractError(result));
        }
        return errors.Count == 0
            ? Result<IReadOnlyList<T>>.Ok(values)
            : Result<IReadOnlyList<T>>.Fail(CombineErrors([.. errors.Distinct()]));
    }

    private static Error ExtractError<T>(Result<T> result) => result switch
    {
        Result<T>.Invalid(var e) => e,
        Result<T>.NotFound(var e) => e,
        Result<T>.NotModified(var e) => e,
        Result<T>.Conflict(var e) => e,
        Result<T>.Forbidden(var e) => e,
        _ => throw new UnreachableException()
    };

    private static Error CombineErrors(Error[] errors)
        => errors.Length == 1
            ? errors[0]
            : new Error("combined.failure", string.Join("; ", errors.Select(e => e.Message)));
}
