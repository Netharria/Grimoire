// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using LanguageExt;
using LanguageExt.Common;

namespace Grimoire.TestShared;

/// <summary>Assertions on <c>Fin</c>, <c>Validation</c> and settings writes (spec 1.1).</summary>
public static class FinShouldExtensions
{
    extension<T>(Fin<T> fin)
    {
        public T ShouldSucceed()
            => fin is Fin<T>.Succ(var value) ? value : throw new ShouldAssertException($"expected success, got {fin}");

        /// <summary>
        ///     The failure, for asserting its case and message: <c>ShouldFail().ShouldBeOfType&lt;Invalid&gt;()</c>.
        /// </summary>
        public Error ShouldFail()
            => fin is Fin<T>.Fail(var error) ? error : throw new ShouldAssertException($"expected a failure, got {fin}");
    }

    extension<T>(Task<Fin<T>> task)
    {
        public async Task<T> ShouldSucceed() => (await task).ShouldSucceed();

        public async Task<Error> ShouldFail() => (await task).ShouldFail();
    }

    extension<T>(ValueTask<Fin<T>> task)
    {
        public async ValueTask<T> ShouldSucceed() => (await task).ShouldSucceed();

        public async ValueTask<Error> ShouldFail() => (await task).ShouldFail();
    }

    extension<T>(Validation<Error, T> validation)
    {
        public T ShouldSucceed()
            => validation is Validation<Error, T>.Success(var value)
                ? value
                : throw new ShouldAssertException($"expected success, got {validation}");

        /// <summary>The failure: one case, or a <c>ManyErrors</c> when several checks failed.</summary>
        public Error ShouldFail()
            => validation is Validation<Error, T>.Fail(var error)
                ? error
                : throw new ShouldAssertException($"expected a failure, got {validation}");
    }

    extension<T>(ValueTask<Validation<Error, T>> task)
    {
        public async ValueTask<T> ShouldSucceed() => (await task).ShouldSucceed();
    }

    extension<T>(Fin<Write<T>> write)
    {
        /// <summary>
        ///     A settings write that either stored a new value or found it already set, for example when a restarted
        ///     test bot configures again.
        /// </summary>
        public void ShouldBeSetOrUnchanged() => write.ShouldSucceed();

        public T ShouldBeSaved() => write.ShouldSucceed().ShouldBeOfType<Write<T>.Saved>().Value;

        /// <summary>The write found the value already set. Returns its message.</summary>
        public string ShouldBeUnchanged() => write.ShouldSucceed().ShouldBeOfType<Write<T>.Unchanged>().Message;
    }

    extension<T>(Task<Fin<Write<T>>> task)
    {
        public async Task ShouldBeSetOrUnchanged() => (await task).ShouldBeSetOrUnchanged();

        public async Task<T> ShouldBeSaved() => (await task).ShouldBeSaved();

        public async Task<string> ShouldBeUnchanged() => (await task).ShouldBeUnchanged();
    }
}
