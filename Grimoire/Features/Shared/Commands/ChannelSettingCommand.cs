// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using LanguageExt;

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
    /// <param name="isModerationLog">
    ///     The moderation log itself: turning it off isn't logged, because the entry would go to the log just turned off.
    /// </param>
    /// <param name="botNeeds">
    ///     What the bot needs in the channel, <see cref="LogPermissions" /> unless given. With none, any channel will do.
    /// </param>
    public static async Task SetAsync(
        CommandContext ctx,
        GuildLog guildLog,
        ChannelOption option,
        DiscordChannel? channel,
        string name,
        Func<ChannelId?, Task<Fin<Write<Option<ChannelId>>>>> save,
        DiscordPermission[]? botNeeds = null,
        DiscordColor? color = null,
        bool isModerationLog = false)
    {
        var guild = ctx.RequiredGuild;
        botNeeds ??= LogPermissions;

        var choice = ctx.GetChannelOption(option, channel)
            .Bind(selected => selected.Match(
                Some: chosen => botNeeds.Length == 0
                    ? Success<Error, Option<DiscordChannel>>(chosen)
                    : CheckCanPost(guild, chosen, botNeeds).Map(Optional),
                None: () => Success<Error, Option<DiscordChannel>>(None)));
        if (choice is Validation<Error, Option<DiscordChannel>>.Fail(var error))
        {
            await ctx.SendErrorResponseAsync(error);
            return;
        }

        var selected = choice.Match(Fail: _ => None, Succ: chosen => chosen).OrNull();
        await ctx.ReplyResultAsync(
            await save(selected?.GetChannelId()),
            selected is null ? $"Disabled {name}." : $"Updated {name} to {selected.Mention}",
            selected is null && isModerationLog ? null : () => guildLog.SendLogMessageAsync(new GuildLogTextMessage
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
    private static Validation<Error, DiscordChannel> CheckCanPost(DiscordGuild guild, DiscordChannel channel,
        DiscordPermission[] botNeeds)
    {
        if (channel.Type is DiscordChannelType.Category or DiscordChannelType.GuildForum or DiscordChannelType.GuildMedia)
            return new Invalid($"{channel.Mention} can't hold messages. Pick a text channel.");

        var permissions = channel.PermissionsFor(guild.CurrentMember);
        if (!permissions.HasPermission(DiscordPermission.SendMessages))
            return new Invalid($"{guild.CurrentMember.Mention} doesn't have permission to send messages in that channel.");

        var missing = botNeeds.Where(permission => !permissions.HasPermission(permission)).ToArray();
        return missing.Length == 0
            ? Success<Error, DiscordChannel>(channel)
            : new Invalid($"{guild.CurrentMember.Mention} also needs {string.Join(" and ", missing.Select(DisplayName))}"
                          + " in that channel to post these logs.");
    }

    private static string DisplayName(DiscordPermission permission)
        => permission switch
        {
            DiscordPermission.EmbedLinks => "**Embed Links**",
            DiscordPermission.AttachFiles => "**Attach Files**",
            _ => $"**{permission}**"
        };
}
