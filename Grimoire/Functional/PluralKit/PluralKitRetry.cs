// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using LanguageExt;

namespace Grimoire.Functional.PluralKit;

/// <summary>How long a PluralKit lookup may take (spec 1.5, section 3.6).</summary>
public static class PluralKitRetry
{
    /// <summary>Two retries, about 0.5 and 1 s with jitter. Each attempt takes its own token from the bucket.</summary>
    public static Schedule Reads =>
        (Schedule.exponential(new LanguageExt.Duration(500)) & Schedule.recurs(2)) | Schedule.jitter(0.1);

    /// <summary>The whole lookup, queue waits and retries included. Past it, the delete is logged as usual.</summary>
    public static readonly TimeSpan Cap = TimeSpan.FromSeconds(15);

    /// <summary>One HTTP attempt (the PluralKit client's <c>HttpClient.Timeout</c>).</summary>
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(5);
}
