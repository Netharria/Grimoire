// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Domain.Obsolete;

namespace Grimoire.Features.Moderation.SpamFilter.Commands;

[Command("SpamFilter")]
internal class SpamFilterOverrideCommands(
    SpamTrackerModule spamTrackerModule,
    IDbContextFactory<GrimoireDbContext> grimoireDbContextFactory)
{
    private readonly IDbContextFactory<GrimoireDbContext> _grimoireDbContextFactory = grimoireDbContextFactory;
    private readonly SpamTrackerModule _spamTrackerModule = spamTrackerModule;

    [Command("Override")]
    [Description("Overrides the default spam filter settings. Use this to control which channels are filtered.")]
    [RequireUserGuildPermissions(DiscordPermission.ManageChannels)]
    public async Task Override(
        SlashCommandContext ctx,
        [Parameter("Option")] [Description("Override option to set the channel to")]
        SpamFilterOverrideSetting overrideSetting,
        [Parameter("Channel")]
        [Description("The channel to override the spam filter settings of. Leave empty for current channel.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();
        channel ??= ctx.Channel;

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");
        if (!ctx.Guild.Channels.ContainsKey(channel.Id))
            throw new AnticipatedException("That channel does not exist in this server.");


        if (overrideSetting is SpamFilterOverrideSetting.Inherit)
        {
            await this._spamTrackerModule.RemoveOverride(channel.Id, ctx.Guild.Id);
            await ctx.EditReplyAsync(GrimoireColor.Purple, $"Set {channel.Mention} to inherit spam filter settings.");
            return;
        }

        var spamFilterOverride = new SpamFilterOverride
        {
            ChannelId = channel.Id,
            GuildId = ctx.Guild.Id,
            ChannelOption = overrideSetting switch
            {
                SpamFilterOverrideSetting.Always => SpamFilterOverrideOption.AlwaysFilter,
                SpamFilterOverrideSetting.Never => SpamFilterOverrideOption.NeverFilter,
                SpamFilterOverrideSetting.Inherit => throw new ArgumentOutOfRangeException(nameof(overrideSetting),
                    overrideSetting, null),
                _ => throw new NotImplementedException(
                    "A spam filter override option was selected that has not been implemented.")
            }
        };
        await this._spamTrackerModule.AddOrUpdateOverride(spamFilterOverride);


        await ctx.EditReplyAsync(GrimoireColor.Purple, spamFilterOverride.ChannelOption switch
        {
            SpamFilterOverrideOption.AlwaysFilter =>
                $"Will now always filter spam messages from {channel.Mention} and its sub channels/threads.",
            SpamFilterOverrideOption.NeverFilter =>
                $"Will now never filter spam messages from {channel.Mention} and its sub channels/threads.",
            _ => throw new NotImplementedException(
                "A spam filter override option was selected that has not been implemented.")
        });
    }

    [Command("View")]
    [Description("Views spam filter settings.")]
    [RequireUserGuildPermissions(DiscordPermission.ManageChannels)]
    public async Task View(SlashCommandContext ctx)
    {
        await ctx.DeferResponseAsync();
        var dbContext = await this._grimoireDbContextFactory.CreateDbContextAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var spamFilterOverrideString = new StringBuilder();

        await foreach (var spamFilterOverride in dbContext.SpamFilterOverrides
                           .AsNoTracking()
                           .Where(spamFilterOverride => spamFilterOverride.GuildId == ctx.Guild.Id)
                           .AsAsyncEnumerable())
        {
            var channel = ctx.Guild.Channels.GetValueOrDefault(spamFilterOverride.ChannelId)
                          ?? ctx.Guild.Threads.GetValueOrDefault(spamFilterOverride.ChannelId);
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

        await ctx.EditReplyAsync(GrimoireColor.Purple, title: "Spam Filter Override Settings",
            message: spamFilterOverrideString.ToString());
    }
}

public enum SpamFilterOverrideSetting
{
    Always,
    Inherit,
    Never
}
