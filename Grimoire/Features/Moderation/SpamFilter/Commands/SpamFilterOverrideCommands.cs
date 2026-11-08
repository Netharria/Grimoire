// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.SpamFilter.Commands;

[Command("SpamFilter")]
internal class SpamFilterOverrideCommands(
    SettingsModule settingsModule)
{
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("Override")]
    [Description("Overrides the default spam filter settings. Use this to control which channels are filtered.")]
    [RequireUserGuildPermissions(DiscordPermission.ManageChannels)]
    public async Task Override(
        CommandContext ctx,
        [Parameter("option")] [Description("Override option to set the channel to")]
        SpamFilterOverrideSetting overrideSetting,
        [Parameter("channel")]
        [Description("The channel to override the spam filter settings of. Leave empty for current channel.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();
        channel ??= ctx.Channel;

        var guild = ctx.Guild!;
        // guild.Channels holds no threads, and the spam filter honours a thread's override.
        if (channel.GuildId != guild.Id)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "That channel does not exist in this server.");
            return;
        }

        var (option, message) = overrideSetting switch
        {
            SpamFilterOverrideSetting.Always => (SpamFilterOverrideOption.AlwaysFilter,
                $"Will now always filter spam messages from {channel.Mention} and its sub channels/threads."),
            SpamFilterOverrideSetting.Never => (SpamFilterOverrideOption.NeverFilter,
                $"Will now never filter spam messages from {channel.Mention} and its sub channels/threads."),
            SpamFilterOverrideSetting.Inherit => (SpamFilterOverrideOption.Inherit,
                $"{channel.Mention} and its sub channels/threads will now follow the parent channel's spam filter setting."),
            _ => throw new UnreachableException("Invalid spam filter override option.")
        };

        await ctx.ReplyResultAsync(
            await this._settingsModule.SetSpamFilterOverrideAsync(channel.GetChannelId(), guild.GetGuildId(),
                ctx.GetModeratorId(), option),
            message);
    }

    [Command("View")]
    [Description("Views spam filter settings.")]
    [RequireUserGuildPermissions(DiscordPermission.ManageChannels)]
    public async Task View(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        var spamFilterOverrideString = new StringBuilder();

        await foreach (var spamFilterOverride in this._settingsModule.GetAllSpamFilterOverrideAsync(guild.GetGuildId()))
        {
            var channel = await guild.GetChannelOrDefaultAsync(spamFilterOverride.ChannelId);
            if (channel is null)
                continue;

            spamFilterOverrideString.Append(channel.Mention)
                .Append(spamFilterOverride.ChannelOption switch
                {
                    SpamFilterOverrideOption.AlwaysFilter => " - Always Filter",
                    SpamFilterOverrideOption.NeverFilter => " - Never Filter",
                    _ => " - Inherit/Default"
                }).AppendLine();
        }

        await ctx.ReplyAsync(GrimoireColor.Purple, title: "Spam Filter Override Settings",
            message: spamFilterOverrideString.ToString());
    }
}

public enum SpamFilterOverrideSetting
{
    Always,
    Inherit,
    Never
}
