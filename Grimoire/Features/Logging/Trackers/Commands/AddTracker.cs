// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Logging.Trackers.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.MessageLog)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
public sealed class AddTracker(SettingsModule settingsModule, GuildLog guildLog)
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("Track")]
    [Description("Creates a log of a user's activity into the specified channel.")]
    public async Task TrackAsync(SlashCommandContext ctx,
        [Parameter("User")] [Description("The user to log.")]
        DiscordUser user,
        [Parameter("DurationType")] [Description("Select whether the duration will be in minutes hours or days.")]
        DurationType durationType,
        [MinMaxValue(0)] [Parameter("DurationAmount")] [Description("The amount of time the logging will last.")]
        int durationAmount,
        [Parameter("Channel")] [Description("Select the channel to log to. Current channel if left blank.")]
        DiscordChannel? discordChannel = null)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        if (user.Id == ctx.Client.CurrentUser.Id)
        {
            await ctx.EditReplyAsync(message: "Why would I track myself?");
            return;
        }

        if (ctx.Guild.Members.TryGetValue(user.Id, out var member))
            if (member.Permissions.HasPermission(DiscordPermission.ManageGuild))
            {
                await ctx.EditReplyAsync(message: "<_<\n>_>\nI can't track a mod.\n Try someone else");
                return;
            }


        discordChannel ??= ctx.Channel;

        if (!ctx.Guild.Channels.ContainsKey(discordChannel.Id))
        {
            await ctx.EditReplyAsync(
                message: "<_<\n>_>\nThat channel is not on this server.\n Try a different one.");
            return;
        }

        var permissions = discordChannel.PermissionsFor(ctx.Guild.CurrentMember);
        if (!permissions.HasPermission(DiscordPermission.SendMessages))
            throw new AnticipatedException(
                $"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");

        await this._settingsModule.AddTracker(
            user.Id,
            ctx.User.Id,
            ctx.Guild.Id,
            discordChannel.Id,
            durationType.GetTimeSpan(durationAmount));

        await ctx.EditReplyAsync(
            message:
            $"Tracker placed on {user.Mention} in {discordChannel.Mention} for {durationAmount} {durationType}");


        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Description =
                $"{ctx.User.Mention} placed a tracker on {user.Mention} in {discordChannel.Mention} for {durationAmount} {durationType}.",
            Color = GrimoireColor.Purple
        });
    }
}
