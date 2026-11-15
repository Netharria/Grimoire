// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public static class OptionExtensions
{
    /// <summary>
    ///     The value, or <c>null</c> when there is none, for handing an absent reference to code that uses
    ///     <c>null</c> (DSharpPlus, EF). Value types use language-ext's <c>ToNullable()</c>.
    /// </summary>
    public static T? OrNull<T>(this Option<T> option) where T : class
        => option.Match(Some: value => value, None: () => (T?)null);
}
