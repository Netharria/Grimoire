// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Commands;

[SlashRequirePermissions(DiscordPermissions.ManageMessages)]
[SlashCommandGroup("Purge", "Delete several recent messages at once.")]
internal sealed class PurgeCommands : ApplicationCommandModule
{
    [SlashCommand("All", "Deletes all messages.")]
    public static async Task AllAsync(InteractionContext ctx,
        [Minimum(0)] [Maximum(1000)] [Option("Count", "The number of matching messages to delete.")]
        long count)
    {
        await ctx.DeferAsync(true);
        var messagesDeleted = await ctx.Channel
            .PurgeMessagesAsync((int)count, $"{ctx.User.Username} purged these messages.");
        await ctx.EditReplyAsync(GrimoireColor.Green,
            PurgeMessageBuilder(messagesDeleted));
    }

    [SlashCommand("User", "Deletes all messages that were sent by this user.")]
    public static async Task UserAsync(InteractionContext ctx,
        [Option("User", "The user to delete the messages of.")]
        DiscordUser user,
        [Minimum(0)] [Maximum(1000)] [Option("Count", "The number of matching messages to delete.")]
        long count)
    {
        await ctx.DeferAsync(true);
        var messagesDeleted = await ctx.Channel
            .PurgeMessagesAsync((int)count, $"{ctx.User.Mention} purged the messages of {user.Mention}.",
                messages => messages.Author is not null && messages.Author == user);
        await ctx.EditReplyAsync(GrimoireColor.Green,
            PurgeMessageBuilder(messagesDeleted));
    }

    private static string PurgeMessageBuilder(int count)
        => $"Purged {count} {(count == 1 ? "message" : "messages")}";
}
