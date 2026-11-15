// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

/// <summary>
///     What a settings write did (spec 1.1, 3.3). Both cases are successes: a write of the value a setting already has
///     stores nothing, and the caller still treats it as done.
/// </summary>
public abstract record Write<T>
{
    private Write() { }

    public TOut Match<TOut>(Func<T, TOut> saved, Func<string, TOut> unchanged)
        => this switch
        {
            Saved(var value) => saved(value),
            Unchanged(var message) => unchanged(message),
            _ => throw new InvariantViolationException()
        };

    /// <summary>Maps a saved value. An unchanged write stays unchanged, with its message.</summary>
    public Write<TOut> Map<TOut>(Func<T, TOut> mapper)
        => this.Match<Write<TOut>>(
            value => new Write<TOut>.Saved(mapper(value)),
            message => new Write<TOut>.Unchanged(message));

    public sealed record Saved(T Value) : Write<T>;

    /// <summary>The value was already set, so nothing was stored. The message says so.</summary>
    public sealed record Unchanged(string Message) : Write<T>;
}
