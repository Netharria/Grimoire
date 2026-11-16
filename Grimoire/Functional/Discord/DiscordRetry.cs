// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using LanguageExt;

namespace Grimoire.Functional.Discord;

public static class DiscordRetry
{
    /// <summary>
    ///     Three retries, about 0.5, 1 and 2 s with jitter: under 4 s in all (spec 1.4, OQ-2). Only for reads and
    ///     idempotent calls; a call that creates something is never retried (Q25).
    /// </summary>
    public static Schedule Reads =>
        (Schedule.exponential(new LanguageExt.Duration(500)) & Schedule.recurs(3)) | Schedule.jitter(0.1);
}
