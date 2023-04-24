// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Discord.SharedModule
{
    [SlashRequirePermissions(Permissions.ManageMessages)]
    [SlashCommandGroup("Purge", "Delete several recent messages at once.")]
    public class PurgeCommands : ApplicationCommandModule
    {

        [SlashCommand("All", "Deletes all messages.")]
        public static async Task AllAsync(InteractionContext ctx,
            [Minimum(0)]
            [Maximum(1000)]
            [Option("Count", "The number of matching messages to delete.")] long count)
        {
            var messagesDeleted = await ctx.Channel
                .PurgeMessagesAsync((int)count, $"{ctx.User.Username} purged these messages.");
            await ctx.ReplyAsync(GrimoireColor.Green,
                message: PurgeMessageBuilder(messagesDeleted));
        }

        [SlashCommand("User", "Deletes all messages that were sent by this user.")]
        public static async Task UserAsync(InteractionContext ctx,
            [Option("User", "The user to delete the messages of.")] DiscordUser user,
            [Minimum(0)]
            [Maximum(1000)]
            [Option("Count", "The number of matching messages to delete.")] long count)
        {
            var messagesDeleted = await ctx.Channel
                .PurgeMessagesAsync((int)count, $"{ctx.User.Mention} purged the messages of {user.Mention}.",
                messages => messages.Author == user);
            await ctx.ReplyAsync(GrimoireColor.Green,
                message: PurgeMessageBuilder(messagesDeleted));
        }

        private static string PurgeMessageBuilder(int count)
            => $"Purged {count} {(count == 1 ? "message" : "messages")}";
    }
}
