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
using JetBrains.Annotations;

namespace Grimoire.Features.Shared.Commands;

[RequirePermissions(DiscordPermission.ManageMessages)]
[Command("Purge")]
[Description("Delete several recent messages at once.")]
internal sealed class PurgeCommands(GuildLog guildLog)
{
    private readonly GuildLog _guildLog = guildLog;

    [UsedImplicitly]
    [Command("All")]
    [Description("Deletes all messages in the channel.")]
    public async Task AllAsync(CommandContext ctx,
        [MinMaxValue(1, 1000)] [Parameter("count")] [Description("The number of messages to delete.")]
        int count)
    {
        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(true);
        else
            await ctx.DeferResponseAsync();
        var messagesDeleted = await ctx.Channel
            .PurgeMessagesAsync(count, $"{ctx.User.Username} purged these messages.");
        await ctx.ReplyAsync(GrimoireColor.Green,
            PurgeMessageBuilder(messagesDeleted));
        await this.LogPurgeAsync(ctx, messagesDeleted);
    }

    [UsedImplicitly]
    [Command("User")]
    [Description("Deletes all messages that were sent by this user.")]
    public async Task UserAsync(CommandContext ctx,
        [Parameter("user")] [Description("The user to delete the messages of.")]
        DiscordUser user,
        [MinMaxValue(1, 1000)] [Parameter("count")] [Description("The number of matching messages to delete.")]
        int count)
    {
        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(true);
        else
            await ctx.DeferResponseAsync();
        var messagesDeleted = await ctx.Channel
            .PurgeMessagesAsync(count, $"{ctx.User.Mention} purged the messages of {user.Mention}.",
                messages => messages.Author is not null && messages.Author == user);
        await ctx.ReplyAsync(GrimoireColor.Green,
            PurgeMessageBuilder(messagesDeleted));
        await this.LogPurgeAsync(ctx, messagesDeleted, user);
    }

    private async ValueTask LogPurgeAsync(CommandContext ctx, int messagesDeleted, DiscordUser? target = null)
    {
        if (messagesDeleted == 0 || ctx.Guild is not { } guild)
            return;

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} purged {messagesDeleted} {(messagesDeleted == 1 ? "message" : "messages")}"
                          + (target is null ? "" : $" from {target.Mention}")
                          + $" in {ctx.Channel.Mention}"
        });
    }

    private static string PurgeMessageBuilder(int count)
        => $"Purged {count} {(count == 1 ? "message" : "messages")}";
}
