// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using LanguageExt;

namespace Grimoire.Functional.Data;

/// <summary>When a read is tried again (spec 1.3, section 3.3). Writes, transactions and streams never are.</summary>
public static class DatabaseRetry
{
    /// <summary>
    ///     Three retries, about 100, 200 and 400 ms with jitter: under a second in all. A command's user is waiting,
    ///     a worker's next tick is its retry, and a longer outage is better reported than hidden.
    /// </summary>
    public static Schedule Reads =>
        (Schedule.exponential(new LanguageExt.Duration(100)) & Schedule.recurs(3)) | Schedule.jitter(0.1);

    /// <summary>Only an <see cref="Unavailable" /> whose cause Npgsql or Postgres marks transient. Pure.</summary>
    public static bool IsTransient(Error error) =>
        error is Unavailable unavailable
        && unavailable.Inner.Bind(inner => inner.Exception).Case is DatabaseException { IsTransient: true };
}
