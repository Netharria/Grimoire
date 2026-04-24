// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public static class Result
{
    public static async Task<Result<(T1, T2)>> WhenAll<T1, T2>(
        Task<Result<T1>> t1,
        Task<Result<T2>> t2)
    {
        await Task.WhenAll(t1, t2);
        if (t1.Result is not Result<T1>.Success(var v1)) return t1.Result.Map<(T1, T2)>(_ => default!);
        if (t2.Result is not Result<T2>.Success(var v2)) return t2.Result.Map<(T1, T2)>(_ => default!);
        return Result<(T1, T2)>.Ok((v1, v2));
    }

    public static async Task<Result<(T1, T2, T3)>> WhenAll<T1, T2, T3>(
        Task<Result<T1>> t1,
        Task<Result<T2>> t2,
        Task<Result<T3>> t3)
    {
        await Task.WhenAll(t1, t2, t3);
        if (t1.Result is not Result<T1>.Success(var v1)) return t1.Result.Map<(T1, T2, T3)>(_ => default!);
        if (t2.Result is not Result<T2>.Success(var v2)) return t2.Result.Map<(T1, T2, T3)>(_ => default!);
        if (t3.Result is not Result<T3>.Success(var v3)) return t3.Result.Map<(T1, T2, T3)>(_ => default!);
        return Result<(T1, T2, T3)>.Ok((v1, v2, v3));
    }

    public static async Task<Result<(T1, T2, T3, T4)>> WhenAll<T1, T2, T3, T4>(
        Task<Result<T1>> t1,
        Task<Result<T2>> t2,
        Task<Result<T3>> t3,
        Task<Result<T4>> t4)
    {
        await Task.WhenAll(t1, t2, t3, t4);
        if (t1.Result is not Result<T1>.Success(var v1)) return t1.Result.Map<(T1, T2, T3, T4)>(_ => default!);
        if (t2.Result is not Result<T2>.Success(var v2)) return t2.Result.Map<(T1, T2, T3, T4)>(_ => default!);
        if (t3.Result is not Result<T3>.Success(var v3)) return t3.Result.Map<(T1, T2, T3, T4)>(_ => default!);
        if (t4.Result is not Result<T4>.Success(var v4)) return t4.Result.Map<(T1, T2, T3, T4)>(_ => default!);
        return Result<(T1, T2, T3, T4)>.Ok((v1, v2, v3, v4));
    }

    public static async Task<Result<(T1, T2, T3, T4, T5)>> WhenAll<T1, T2, T3, T4, T5>(
        Task<Result<T1>> t1,
        Task<Result<T2>> t2,
        Task<Result<T3>> t3,
        Task<Result<T4>> t4,
        Task<Result<T5>> t5)
    {
        await Task.WhenAll(t1, t2, t3, t4, t5);
        if (t1.Result is not Result<T1>.Success(var v1)) return t1.Result.Map<(T1, T2, T3, T4, T5)>(_ => default!);
        if (t2.Result is not Result<T2>.Success(var v2)) return t2.Result.Map<(T1, T2, T3, T4, T5)>(_ => default!);
        if (t3.Result is not Result<T3>.Success(var v3)) return t3.Result.Map<(T1, T2, T3, T4, T5)>(_ => default!);
        if (t4.Result is not Result<T4>.Success(var v4)) return t4.Result.Map<(T1, T2, T3, T4, T5)>(_ => default!);
        if (t5.Result is not Result<T5>.Success(var v5)) return t5.Result.Map<(T1, T2, T3, T4, T5)>(_ => default!);
        return Result<(T1, T2, T3, T4, T5)>.Ok((v1, v2, v3, v4, v5));
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
        if (t1.Result is not Result<T1>.Success(var v1)) return t1.Result.Map<(T1, T2, T3, T4, T5, T6)>(_ => default!);
        if (t2.Result is not Result<T2>.Success(var v2)) return t2.Result.Map<(T1, T2, T3, T4, T5, T6)>(_ => default!);
        if (t3.Result is not Result<T3>.Success(var v3)) return t3.Result.Map<(T1, T2, T3, T4, T5, T6)>(_ => default!);
        if (t4.Result is not Result<T4>.Success(var v4)) return t4.Result.Map<(T1, T2, T3, T4, T5, T6)>(_ => default!);
        if (t5.Result is not Result<T5>.Success(var v5)) return t5.Result.Map<(T1, T2, T3, T4, T5, T6)>(_ => default!);
        if (t6.Result is not Result<T6>.Success(var v6)) return t6.Result.Map<(T1, T2, T3, T4, T5, T6)>(_ => default!);
        return Result<(T1, T2, T3, T4, T5, T6)>.Ok((v1, v2, v3, v4, v5, v6));
    }
}
