// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

/// <summary>
///     Grimoire reached a case its own code treats as impossible: a bug in Grimoire, not bad input. Used instead of
///     <see cref="System.Diagnostics.UnreachableException" /> so it can't be confused with one thrown by a library.
/// </summary>
/// <remarks>
///     The message is always text written here in the code. Never put user content, tokens or other data from Discord
///     or the database in it.
/// </remarks>
public sealed class InvariantViolationException(string message = "Reached a case the code treats as impossible.")
    : Exception(message);
