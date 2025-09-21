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

    [GeneratedRegex("<@!(\\d+)>", RegexOptions.ECMAScript)]
    private static partial Regex NicknameMentionRegex();

    [GeneratedRegex("<#(\\d+)>", RegexOptions.ECMAScript)]
    private static partial Regex ChannelMentionRegex();

    [GeneratedRegex("<@&(\\d+)>", RegexOptions.ECMAScript)]
    private static partial Regex RoleMentionRegex();

    [GeneratedRegex("<a?:(.*):(\\d+)>", RegexOptions.ECMAScript)]
    private static partial Regex EmojiMentionRegex();

    [GeneratedRegex("^[\\w-]{1,32}$")]
    private static partial Regex SlashCommandNameRegex();

    public static bool ContainsUserMentions(string message)
    {
        var regex = UserMentionRegex();
        return regex.IsMatch(message);
    }

    public static bool ContainsNicknameMentions(string message)
    {
        var regex = NicknameMentionRegex();
        return regex.IsMatch(message);
    }

    public static bool ContainsChannelMentions(string message)
    {
        var regex = ChannelMentionRegex();
        return regex.IsMatch(message);
    }

    public static bool ContainsRoleMentions(string message)
    {
        var regex = RoleMentionRegex();
        return regex.IsMatch(message);
    }

    public static bool ContainsEmojis(string message)
    {
        var regex = EmojiMentionRegex();
        return regex.IsMatch(message);
    }

    public static IEnumerable<ulong> GetUserMentions(DiscordMessage message)
    {
        var regex = UserMentionRegex();
        var matches = regex.Matches(message.Content);
        foreach (var match in matches.Cast<Match>())
            yield return ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    public static IEnumerable<ulong> GetUserMentions(string message)
    {
        var regex = UserMentionRegex();
        var matches = regex.Matches(message);
        foreach (var match in matches.Cast<Match>())
            yield return ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    public static IEnumerable<ulong> GetRoleMentions(DiscordMessage message)
    {
        var regex = RoleMentionRegex();
        var matches = regex.Matches(message.Content);
        foreach (var match in matches.Cast<Match>())
            yield return ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    public static IEnumerable<ulong> GetRoleMentions(string message)
    {
        var regex = RoleMentionRegex();
        var matches = regex.Matches(message);
        foreach (var match in matches.Cast<Match>())
            yield return ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    public static IEnumerable<ulong> GetChannelMentions(DiscordMessage message) => GetChannelMentions(message.Content);

    public static IEnumerable<ulong> GetChannelMentions(string messageContent)
    {
        var regex = ChannelMentionRegex();
        var matches = regex.Matches(messageContent);
        foreach (var match in matches.Cast<Match>())
            yield return ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    public static IEnumerable<ulong> GetEmojis(DiscordMessage message)
    {
        var regex = EmojiMentionRegex();
        var matches = regex.Matches(message.Content);
        foreach (var match in matches.Cast<Match>())
            yield return ulong.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
    }

    public static bool IsValidSlashCommandName(string name)
    {
        var regex = SlashCommandNameRegex();
        return regex.IsMatch(name);
    }
}
