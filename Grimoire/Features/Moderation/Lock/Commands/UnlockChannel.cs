// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Lock.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequirePermissions([DiscordPermission.ManageChannels], [DiscordPermission.ManageMessages])]
public sealed class UnlockChannel(SettingsModule settingsModule, GuildLog guildLog)
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("Unlock")]
    [Description("Unlocks a channel.")]
    public async Task UnlockChannelAsync(
        CommandContext ctx,
        [Parameter("Channel")] [Description("The channel to unlock. Current channel if not specified.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;
        channel ??= ctx.Channel;
        var moderatorId = ctx.GetModeratorId();

        await Validation<DiscordChannel>.Succeed(channel)
            .ToResult()
            .BindAsync(discordChannel =>
                discordChannel switch
                {
                    { IsThread:true } => TryUnlockThreadAsync(guild, discordChannel, moderatorId).Map(_ => ctx),
                    _ => TryUnlockChannelAsync(guild, discordChannel, moderatorId).Map(_ => ctx)
                })
            .TapAsync(context => context.ReplyAsync(message: $"{channel.Mention} has been unlocked").AsTask())
            .Match(
                onSuccess: _ => this._guildLog.SendLogMessageAsync(new GuildLogMessage
                    {
                        GuildId = guild.GetGuildId(),
                        GuildLogType = GuildLogType.Moderation,
                        Color = GrimoireColor.Purple,
                        Description = $"{ctx.User.Mention} unlocked {channel.Mention}"
                    }),
                onFail: _ => ctx.ReplyAsync(message: $"{channel.Mention} could not be unlocked"),
                onNotFound: _ => ctx.ReplyAsync(message: $"{channel.Mention} is not locked.")
                );


    }

    private Task<Result<ThreadLocked>> TryUnlockThreadAsync(DiscordGuild guild, DiscordChannel channel, ModeratorId moderatorId)
    => ThreadUnlocked.Create(moderatorId, channel.GetChannelId(), guild.GetGuildId(), DateTimeOffset.UtcNow)
            .ToResult()
            .BindAsync(lockAction => this._settingsModule.ApplyThreadLockAction(lockAction));

    private Task<Result<ChannelLocked>> TryUnlockChannelAsync(DiscordGuild guild, DiscordChannel channel,
        ModeratorId moderatorId)
        => ChannelUnlocked.Create(moderatorId, channel.GetChannelId(), guild.GetGuildId(), DateTimeOffset.UtcNow)
            .ToResult()
            .BindAsync(lockAction => this._settingsModule.ApplyChannelLockAction(lockAction))
            .TapAsync(lockAction =>
            {
                var permissions = guild.Channels[channel.Id].PermissionOverwrites
                    .First(x => x.Id == guild.EveryoneRole.Id);
                return channel.AddOverwriteAsync(guild.EveryoneRole,
                    permissions.Allowed.RevertLockPermissions(lockAction.PreviouslyAllowed.Permissions),
                    permissions.Denied.RevertLockPermissions(lockAction.PreviouslyDenied.Permissions));
            });
}
