// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Interactivity.Extensions;

namespace Grimoire.Discord;
public class ExampleContextMenus : ApplicationCommandModule
{
    [ContextMenu(ApplicationCommandType.MessageContextMenu, "Delete")]
    public static async Task DeleteMessageAsync(ContextMenuContext ctx)
    {
        await ctx.TargetMessage.DeleteAsync();
        await ctx.CreateResponseAsync(new DiscordEmbedBuilder().WithDescription("Message was deleted"));
    }

    [ContextMenu(ApplicationCommandType.UserContextMenu, "ChannelBan")]
    public static async Task ChannelBanAsync(ContextMenuContext ctx)
    {
        await ctx.Channel.AddOverwriteAsync(ctx.TargetMember, Permissions.None, Permissions.SendMessages);
        await ctx.CreateResponseAsync(new DiscordEmbedBuilder().WithDescription("User was channel banned."));
    }

    [ContextMenu(ApplicationCommandType.MessageContextMenu, "Settings")]
    public static async Task MessageSettingsAsync(ContextMenuContext ctx)
    {
        await ctx.DeferAsync();
        var rows = new List<DiscordActionRowComponent>()
        {
            new(new List<DiscordComponent>
            {
                new DiscordChannelSelectComponent("channel_option", "",
                new List<ChannelType> { ChannelType.Text })
            }),
            new(new List<DiscordComponent>
            {
                new DiscordRoleSelectComponent("role_option", "")
            }),
            new(new List<DiscordComponent>
            {
                new DiscordUserSelectComponent("user_option", "")
            }),
            new(new List<DiscordComponent>
            {
                new DiscordMentionableSelectComponent("mentionable_option", "")
            })
        };
        var builder = new DiscordFollowupMessageBuilder()
            .WithContent("Select an option")
            .AddComponents(rows);
        var message = await ctx.FollowUpAsync(builder);
        await message.WaitForSelectAsync("OptionSelect");
        await ctx.EditFollowupAsync(message.Id, new DiscordWebhookBuilder().WithContent("Thanks"));
    }
}
