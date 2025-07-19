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
        Regex regex = UserMentionRegex();
        return regex.IsMatch(message);
    }

    public static bool ContainsNicknameMentions(string message)
    {
        Regex regex = NicknameMentionRegex();
        return regex.IsMatch(message);
    }

    public static bool ContainsChannelMentions(string message)
    {
        Regex regex = ChannelMentionRegex();
        return regex.IsMatch(message);
    }

    public static bool ContainsRoleMentions(string message)
    {
        Regex regex = RoleMentionRegex();
        return regex.IsMatch(message);
    }

    public static bool ContainsEmojis(string message)
    {
        Regex regex = EmojiMentionRegex();
        return regex.IsMatch(message);
    }

    public static IEnumerable<ulong> GetUserMentions(DiscordMessage message)
    {
        Regex regex = UserMentionRegex();
        MatchCollection matches = regex.Matches(message.Content);
        foreach (Match match in matches.Cast<Match>())
        {
            yield return ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }

    public static IEnumerable<ulong> GetUserMentions(string message)
    {
        Regex regex = UserMentionRegex();
        MatchCollection matches = regex.Matches(message);
        foreach (Match match in matches.Cast<Match>())
        {
            yield return ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }

    public static IEnumerable<ulong> GetRoleMentions(DiscordMessage message)
    {
        Regex regex = RoleMentionRegex();
        MatchCollection matches = regex.Matches(message.Content);
        foreach (Match match in matches.Cast<Match>())
        {
            yield return ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }

    public static IEnumerable<ulong> GetRoleMentions(string message)
    {
        Regex regex = RoleMentionRegex();
        MatchCollection matches = regex.Matches(message);
        foreach (Match match in matches.Cast<Match>())
        {
            yield return ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }

    public static IEnumerable<ulong> GetChannelMentions(DiscordMessage message) => GetChannelMentions(message.Content);

    public static IEnumerable<ulong> GetChannelMentions(string messageContent)
    {
        Regex regex = ChannelMentionRegex();
        MatchCollection matches = regex.Matches(messageContent);
        foreach (Match match in matches.Cast<Match>())
        {
            yield return ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }

    public static IEnumerable<ulong> GetEmojis(DiscordMessage message)
    {
        Regex regex = EmojiMentionRegex();
        MatchCollection matches = regex.Matches(message.Content);
        foreach (Match match in matches.Cast<Match>())
        {
            yield return ulong.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        }
    }

    public static bool IsValidSlashCommandName(string name)
    {
        Regex regex = SlashCommandNameRegex();
        return regex.IsMatch(name);
    }
}
