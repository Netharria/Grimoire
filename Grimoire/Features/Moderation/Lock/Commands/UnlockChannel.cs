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
using LanguageExt;

namespace Grimoire.Features.Moderation.Lock.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequirePermissions([DiscordPermission.ManageChannels], [DiscordPermission.ManageMessages])]
public sealed class UnlockChannel(SettingsModule settingsModule, GuildLog guildLog)
{
    [Command("Unlock")]
    [Description("Unlocks a channel.")]
    public async Task UnlockChannelAsync(
        CommandContext ctx,
        [Parameter("channel")] [Description("The channel to unlock. Current channel if not specified.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;
        channel ??= ctx.Channel;
        var moderatorId = ctx.GetModeratorId();

        if (LockTargetPermissions.CheckModerator(ctx.RequiredMember, channel) is Fin<Unit>.Fail(var denied))
        {
            await ctx.SendErrorResponseAsync(denied);
            return;
        }

        await (channel.IsThread
                ? this.TryUnlockThreadAsync(guild, channel, moderatorId).MapFin(_ => unit)
                : this.TryUnlockChannelAsync(guild, channel, moderatorId).MapFin(_ => unit))
            .TapAsync(_ => ctx.ReplyAsync(message: $"{channel.Mention} has been unlocked").AsTask())
            .Match(
                _ => guildLog.SendLogMessageAsync(new GuildLogMessage
                {
                    GuildId = guild.GetGuildId(),
                    GuildLogType = GuildLogType.Moderation,
                    Color = GrimoireColor.Purple,
                    Description = $"{ctx.User.Mention} unlocked {channel.Mention}"
                }),
                error => error is NotFound
                    ? ctx.ReplyAsync(message: $"{channel.Mention} is not locked.")
                    : ctx.ReplyAsync(message: $"{channel.Mention} could not be unlocked"));
    }

    private Task<Fin<ThreadLocked>> TryUnlockThreadAsync(DiscordGuild guild, DiscordChannel channel,
        ModeratorId moderatorId)
        => settingsModule.RemoveThreadLock(
            ThreadUnlocked.Create(moderatorId, channel.GetChannelId(), guild.GetGuildId(), DateTimeOffset.UtcNow));

    /// <summary>
    ///     Reverts the channel's permissions first and only then records the unlock. If Discord refuses, the channel is
    ///     still recorded as locked, so it can be unlocked again or the expiry task will finish the job.
    /// </summary>
    private async Task<Fin<ChannelLocked>> TryUnlockChannelAsync(DiscordGuild guild, DiscordChannel channel,
        ModeratorId moderatorId)
        => await (await settingsModule.GetActiveChannelLockAsync(channel.GetChannelId(), guild.GetGuildId()))
            .BindAsync(async activeLock =>
            {
                await ChannelLockReverter.RevertAsync(channel, guild.EveryoneRole, activeLock);
                return await settingsModule.RemoveChannelLock(ChannelUnlocked.Create(moderatorId,
                    channel.GetChannelId(), guild.GetGuildId(), DateTimeOffset.UtcNow));
            });
}
