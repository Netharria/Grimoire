// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Shared.Commands;

/// <summary>The body every command shares that points a setting at a channel, or turns it off.</summary>
internal static class ChannelSettingCommand
{
    /// <param name="name">The setting as the reply names it, such as "the moderation log".</param>
    /// <param name="save">Saves the chosen channel, or null for off.</param>
    /// <param name="botMustSend">Refuse a channel the bot can't send messages in, as a log channel would be.</param>
    public static async Task SetAsync(
        CommandContext ctx,
        GuildLog guildLog,
        ChannelOption option,
        DiscordChannel? channel,
        string name,
        Func<ChannelId?, Task<Result<ChannelId?>>> save,
        bool botMustSend = true,
        DiscordColor? color = null)
    {
        var guild = ctx.Guild!;

        var choice = ctx.GetChannelOption(option, channel)
            .Bind(selected => botMustSend && selected is not null
                              && !selected.PermissionsFor(guild.CurrentMember).HasPermission(DiscordPermission.SendMessages)
                ? Validation<DiscordChannel?>.Fail(new Error("channel.bot-cannot-send",
                    $"{guild.CurrentMember.Mention} doesn't have permission to send messages in that channel."))
                : Validation<DiscordChannel?>.Succeed(selected));
        if (choice is Validation<DiscordChannel?>.Invalid { Errors: var errors })
        {
            await ctx.SendErrorResponseAsync(errors);
            return;
        }

        var selected = choice.GetOrElse(() => null);
        await ctx.ReplyResultAsync(
            await save(selected?.GetChannelId()),
            selected is null ? $"Disabled {name}." : $"Updated {name} to {selected.Mention}",
            () => guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = guild.GetGuildId(),
                GuildLogType = GuildLogType.Moderation,
                Color = color ?? GrimoireColor.Purple,
                Description = selected is null
                    ? $"{ctx.User.Mention} disabled {name}."
                    : $"{ctx.User.Mention} updated {name} to {selected.Mention}."
            }),
            color);
    }
}
