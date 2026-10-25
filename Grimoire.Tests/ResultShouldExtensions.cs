// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Tests;

public static class ResultShouldExtensions
{
    public static T ShouldSucceed<T>(this Result<T> result)
        => result.ShouldBeOfType<Result<T>.Success>().Value;

    public static async Task<T> ShouldSucceed<T>(this Task<Result<T>> resultTask)
        => (await resultTask).ShouldBeOfType<Result<T>.Success>().Value;

    public static async ValueTask<T> ShouldSucceed<T>(this ValueTask<Result<T>> resultTask)
        => (await resultTask).ShouldBeOfType<Result<T>.Success>().Value;

    /// <summary>
    ///     A settings write that either stored a new value or found it already set. Settings return
    ///     <c>NotModified</c> when the value is unchanged, for example when a restarted test bot configures again.
    /// </summary>
    public static void ShouldBeSetOrUnchanged<T>(this Result<T> result)
        => result.ShouldSatisfyAllConditions(
            () => (result is Result<T>.Success or Result<T>.NotModified).ShouldBeTrue($"the settings write failed: {result}"));

    public static T ShouldSucceed<T>(this Validation<T> validation)
        => validation.ShouldBeOfType<Validation<T>.Valid>().Value;

    public static async ValueTask<T> ShouldSucceed<T>(this ValueTask<Validation<T>> validation)
        => (await validation).ShouldBeOfType<Validation<T>.Valid>().Value;
}
