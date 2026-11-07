// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation;

internal static class ModerationDirectMessage
{
    /// <summary>
    ///     Tells a member about an action taken against them. Members can close their DMs, so a failure goes to the
    ///     mod log instead of failing the command.
    /// </summary>
    public static async ValueTask DirectMessageAsync(this GuildLog guildLog, DiscordMember member, DiscordEmbed embed,
        string action)
    {
        // Bots can't DM each other. DSharpPlus throws ArgumentException before sending, which would stop the action.
        if (member.IsBot)
            return;

        try
        {
            await member.SendMessageAsync(embed);
        }
        catch (DiscordException)
        {
            await guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = member.GetGuildId(),
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.Red,
                Description = $"Was not able to send a direct message with the {action} details to {member.Mention}"
            });
        }
    }

    /// <summary>The closing words of a DM, which only exist when the moderator gave a reason.</summary>
    public static string ForReason(string? reason) => string.IsNullOrWhiteSpace(reason) ? "" : $" for {reason}";
}
