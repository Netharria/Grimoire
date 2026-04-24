// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests;

public static class ResultShouldExtensions
{
    public static T ShouldSucceed<T>(this Result<T> result)
        => result.ShouldBeOfType<Result<T>.Success>().Value;

    public static async Task<T> ShouldSucceed<T>(this Task<Result<T>> resultTask)
        => (await resultTask).ShouldBeOfType<Result<T>.Success>().Value;

    public static async ValueTask<T> ShouldSucceed<T>(this ValueTask<Result<T>> resultTask)
        => (await resultTask).ShouldBeOfType<Result<T>.Success>().Value;

    public static T ShouldSucceed<T>(this Validation<T> validation)
        => validation.ShouldBeOfType<Validation<T>.Valid>().Value;


    public static async ValueTask<T> ShouldSucceed<T>(this ValueTask<Validation<T>> validation)
        => (await validation).ShouldBeOfType<Validation<T>.Valid>().Value;
}
