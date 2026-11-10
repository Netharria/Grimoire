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

namespace Grimoire.Features.Moderation.Ban.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
[RequirePermissions([DiscordPermission.BanMembers], [])]
public sealed class AddBanCommand(BanHandoff banHandoff, GuildLog guildLog, SettingsModule settingsModule)
{
    [Command("Ban")]
    [Description("Bans a user from the server.")]
    public async Task BanAsync(
        CommandContext ctx,
        [Parameter("user")] [Description("The user to ban.")]
        DiscordUser user,
        [MinMaxLength(maxLength: 1000)]
        [Parameter("reason")]
        [Description("The reason for the ban. This can be updated later with the 'Reason' command.")]
        string reason = "",
        [Parameter("delete_messages")]
        [Description("Deletes the messages of the user of the last few days. Default is false.")]
        bool deleteMessages = false,
        [MinMaxValue(0, 7)]
        [Parameter("delete_days")]
        [Description("The number of days of messages to delete. Default is 7.")]
        int deleteDays = 7)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        if (ctx.User.Id == user.Id)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "You can't ban yourself.");
            return;
        }

        var member = await guild.GetMemberOrDefaultAsync(user.GetUserId());
        if (member is not null
            && await ModerationRank.GetRefusalReasonAsync(settingsModule, ctx, member, "ban") is { } refusal)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, refusal);
            return;
        }

        if (member is not null)
            await guildLog.DirectMessageAsync(member, new DiscordEmbedBuilder()
                .WithAuthor("Banned")
                .WithDescription($"You have been banned from {guild.Name}{ModerationDirectMessage.ForReason(reason)}")
                .WithColor(GrimoireColor.Red), "ban");

        var pending = banHandoff.ExpectBan(guild.GetGuildId(), user.GetUserId(), ctx.GetModeratorId(), reason);
        try
        {
            await guild.BanMemberAsync(
                user,
                deleteMessages ? TimeSpan.FromDays(deleteDays) : TimeSpan.Zero,
                reason);
        }
        catch (Exception)
        {
            banHandoff.CancelBan(guild.GetGuildId(), user.GetUserId());
            throw;
        }

        var sinId = await pending.WaitUntilRecordedAsync();

        await ctx.ReplyAsync(embed: new DiscordEmbedBuilder()
            .WithAuthor("Banned")
            .AddField("User", user.Mention, true)
            .AddField("Sin Id", sinId is { } id ? $"**{id}**" : "Not recorded yet", true)
            .AddField("Moderator", ctx.User.Mention, true)
            .AddField("Reason", string.IsNullOrWhiteSpace(reason) ? "None" : reason)
            .WithColor(GrimoireColor.Red)
            .WithTimestamp(DateTimeOffset.UtcNow));
    }
}
