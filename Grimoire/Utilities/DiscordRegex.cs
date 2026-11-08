// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Grimoire.Utilities;

public static partial class DiscordRegex
{
    [GeneratedRegex("<@(\\d+)>", RegexOptions.ECMAScript)]
    private static partial Regex UserMentionRegex();

    [GeneratedRegex("<@&(\\d+)>", RegexOptions.ECMAScript)]
    private static partial Regex RoleMentionRegex();

    public static IEnumerable<ulong> GetUserMentions(string message) => Ids(UserMentionRegex(), message);

    public static IEnumerable<ulong> GetRoleMentions(string message) => Ids(RoleMentionRegex(), message);

    private static IEnumerable<ulong> Ids(Regex regex, string message)
        => regex.Matches(message).Select(match => ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
}
