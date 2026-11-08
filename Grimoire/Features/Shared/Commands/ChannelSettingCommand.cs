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
    /// <summary>What every log needs: each one is an embed.</summary>
    public static readonly DiscordPermission[] LogPermissions = [DiscordPermission.SendMessages, DiscordPermission.EmbedLinks];

    /// <summary>For logs that upload files: deleted images, the bulk delete dump, avatars.</summary>
    public static readonly DiscordPermission[] LogWithFilesPermissions = [.. LogPermissions, DiscordPermission.AttachFiles];

    /// <param name="name">The setting as the reply names it, such as "the moderation log".</param>
    /// <param name="save">Saves the chosen channel, or null for off.</param>
    /// <param name="botNeeds">
    ///     What the bot needs in the channel, <see cref="LogPermissions" /> unless given. With none, any channel will do.
    /// </param>
    public static async Task SetAsync(
        CommandContext ctx,
        GuildLog guildLog,
        ChannelOption option,
        DiscordChannel? channel,
        string name,
        Func<ChannelId?, Task<Result<ChannelId?>>> save,
        DiscordPermission[]? botNeeds = null,
        DiscordColor? color = null)
    {
        var guild = ctx.Guild!;
        botNeeds ??= LogPermissions;

        var choice = ctx.GetChannelOption(option, channel)
            .Bind(selected => selected is null || botNeeds.Length == 0
                ? Validation<DiscordChannel?>.Succeed(selected)
                : CheckCanPost(guild, selected, botNeeds));
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

    /// <summary>
    ///     Refuses a channel the bot can't post these logs in. A wrong choice would otherwise only show up as a setup
    ///     problem once the first log fails.
    /// </summary>
    private static Validation<DiscordChannel?> CheckCanPost(DiscordGuild guild, DiscordChannel channel,
        DiscordPermission[] botNeeds)
    {
        if (channel.Type is DiscordChannelType.Category or DiscordChannelType.GuildForum or DiscordChannelType.GuildMedia)
            return Validation<DiscordChannel?>.Fail(new Error("channel.cannot-hold-messages",
                $"{channel.Mention} can't hold messages. Pick a text channel."));

        var permissions = channel.PermissionsFor(guild.CurrentMember);
        if (!permissions.HasPermission(DiscordPermission.SendMessages))
            return Validation<DiscordChannel?>.Fail(new Error("channel.bot-cannot-send",
                $"{guild.CurrentMember.Mention} doesn't have permission to send messages in that channel."));

        var missing = botNeeds.Where(permission => !permissions.HasPermission(permission)).ToArray();
        return missing.Length == 0
            ? Validation<DiscordChannel?>.Succeed(channel)
            : Validation<DiscordChannel?>.Fail(new Error("channel.bot-permissions",
                $"{guild.CurrentMember.Mention} also needs {string.Join(" and ", missing.Select(DisplayName))} in that channel to post these logs."));
    }

    private static string DisplayName(DiscordPermission permission)
        => permission switch
        {
            DiscordPermission.EmbedLinks => "**Embed Links**",
            DiscordPermission.AttachFiles => "**Attach Files**",
            _ => $"**{permission}**"
        };
}
