// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using JetBrains.Annotations;

namespace Grimoire.Features.Shared.Commands;

[RequirePermissions(DiscordPermission.ManageMessages)]
[Command("Purge")]
[Description("Delete several recent messages at once.")]
internal sealed class PurgeCommands
{
    [UsedImplicitly]
    [Command("All")]
    [Description("Deletes all messages in the channel.")]
    public static async Task AllAsync(SlashCommandContext ctx,
        [MinMaxValue(0, 1000)] [Parameter("Count")] [Description("The number of messages to delete.")]
        int count)
    {
        await ctx.DeferResponseAsync(true);
        var messagesDeleted = await ctx.Channel
            .PurgeMessagesAsync(count, $"{ctx.User.Username} purged these messages.");
        await ctx.EditReplyAsync(GrimoireColor.Green,
            PurgeMessageBuilder(messagesDeleted));
    }

    [UsedImplicitly]
    [Command("User")]
    [Description("Deletes all messages that were sent by this user.")]
    public static async Task UserAsync(SlashCommandContext ctx,
        [Parameter("User")] [Description("The user to delete the messages of.")]
        DiscordUser user,
        [MinMaxValue(0, 1000)] [Parameter("Count")] [Description("The number of matching messages to delete.")]
        int count)
    {
        await ctx.DeferResponseAsync(true);
        var messagesDeleted = await ctx.Channel
            .PurgeMessagesAsync(count, $"{ctx.User.Mention} purged the messages of {user.Mention}.",
                messages => messages.Author is not null && messages.Author == user);
        await ctx.EditReplyAsync(GrimoireColor.Green,
            PurgeMessageBuilder(messagesDeleted));
    }

    private static string PurgeMessageBuilder(int count)
        => $"Purged {count} {(count == 1 ? "message" : "messages")}";
}
