// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Threading.Channels;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels;

namespace Grimoire.Features.Leveling.Settings;

[RequireGuild]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
[RequireModuleEnabled(Module.Leveling)]
[Command("Ignore")]
[Description("Commands for updating and viewing the server ignore list.")]
public sealed partial class IgnoreCommandGroup(IMediator mediator, Channel<PublishToGuildLog> channel)
{
    private readonly IMediator _mediator = mediator;
    private readonly Channel<PublishToGuildLog> _channel = channel;

    [Command("Add")]
    [Description("Adds a user, channel, or role to the ignored xp list.")]
    public Task IgnoreAsync(CommandContext ctx,
        [Parameter("items")]
        [Description("The user, channel or role to ignore")]
        params SnowflakeObject[] value) =>
        this.UpdateIgnoreState(ctx, value, true);

    [Command("Remove")]
    [Description("Removes a user, channel, or role from the ignored xp list.")]
    public Task WatchAsync(CommandContext ctx,
        [Parameter("Item")]
        [Description("The user, channel or role to remove from the ignore xp list.")]
        params SnowflakeObject[]  value) =>
        this.UpdateIgnoreState(ctx, value, false);

    private async Task UpdateIgnoreState(CommandContext ctx, SnowflakeObject[] value, bool shouldIgnore)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");


        if (value.Length == 0)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "Could not parse any ids from the submitted values.");
            return;
        }

        IUpdateIgnoreForXpGain command = shouldIgnore
            ? new AddIgnoreForXpGain.Command { GuildId = ctx.Guild.Id }
            : new RemoveIgnoreForXpGain.Command { GuildId = ctx.Guild.Id };

        command.Users = await value
            .OfType<DiscordUser>()
            .ToAsyncEnumerable()
            .SelectAwait(async user => await BuildUserDto(ctx.Client, user.Id, ctx.Guild.Id))
            .OfType<UserDto>()
            .ToArrayAsync();

        command.Roles = value
            .OfType<DiscordRole>()
            .Select(x =>
                new RoleDto { Id = x.Id, GuildId = ctx.Guild.Id }).ToArray();

        command.Channels = value
            .OfType<DiscordChannel>()
            .Select(x =>
                new ChannelDto { Id = x.Id, GuildId = ctx.Guild.Id })
            .ToArray();

        var response = await this._mediator.Send(command);


        await ctx.EditReplyAsync(GrimoireColor.Green,
            string.IsNullOrWhiteSpace(response.Message)
                ? "All items in list provided were not ignored"
                : response.Message);
        await this._channel.Writer.WriteAsync(new PublishToGuildLog
        {
            LogChannelId = response.LogChannelId,
            Color = GrimoireColor.DarkPurple,
            Description = response.Message
        });
    }

    private static async ValueTask<UserDto?> BuildUserDto(DiscordClient client, ulong id, ulong guildId)
    {
        if (client.Guilds[guildId].Members.TryGetValue(id, out var member))
            return new UserDto
            {
                Id = member.Id,
                Nickname = member.Nickname,
                Username = member.GetUsernameWithDiscriminator(),
                AvatarUrl = member.GetGuildAvatarUrl(MediaFormat.Auto)
            };
        try
        {
            var user = await client.GetUserAsync(id);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (user is not null)
                return new UserDto
                {
                    Id = user.Id,
                    Username = user.GetUsernameWithDiscriminator(),
                    AvatarUrl = user.GetAvatarUrl(MediaFormat.Auto)
                };
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }
}
