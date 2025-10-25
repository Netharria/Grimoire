// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Lock.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequirePermissions([DiscordPermission.ManageChannels], [DiscordPermission.ManageMessages])]
public sealed class UnlockChannel(SettingsModule settingsModule, GuildLog guildLog)
{
    private readonly SettingsModule _settingsModule = settingsModule;
    private readonly GuildLog _guildLog = guildLog;

    [Command("Unlock")]
    [Description("Unlocks a channel.")]
    public async Task UnlockChannelAsync(
        SlashCommandContext ctx,
        [Parameter("Channel")] [Description("The channel to unlock. Current channel if not specified.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        channel ??= ctx.Channel;
        var response = await this._settingsModule.RemoveLock(channel.Id, ctx.Guild.Id);

        if (response is null)
        {
            await ctx.EditReplyAsync(message: $"{channel.Mention} is not locked.");
            return;
        }

        if (!channel.IsThread)
        {
            var permissions = ctx.Guild.Channels[channel.Id].PermissionOverwrites
                .First(x => x.Id == ctx.Guild.EveryoneRole.Id);
            await channel.AddOverwriteAsync(ctx.Guild.EveryoneRole,
                permissions.Allowed.RevertLockPermissions(response.PreviouslyAllowed)
                , permissions.Denied.RevertLockPermissions(response.PreviouslyDenied));
        }

        await ctx.EditReplyAsync(message: $"{channel.Mention} has been unlocked");

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} unlocked {channel.Mention}"
        });
    }
}
