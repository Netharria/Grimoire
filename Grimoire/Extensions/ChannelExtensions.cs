// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.Contracts;

namespace Grimoire.Extensions;

public static class ChannelExtensions
{
    [Pure]
    public static string Mention(ChannelId? id)
        => id is not null
            ? $"<#{id}>"
            : "Unknown Channel";

    [Pure]
    public static string Mention(ulong? id)
        => id is not null
            ? $"<#{id}>"
            : "Unknown Channel";
}
