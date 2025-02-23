// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;

namespace Grimoire.Features.Leveling.Settings;

[RequireGuild]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
[RequireModuleEnabled(Module.Leveling)]
[Command("Ignore")]
[Description("Commands for updating and viewing the server ignore list.")]
public sealed partial class IgnoreCommandGroup(IMediator mediator)
{
    private readonly IMediator _mediator = mediator;

    [Command("Add")]
    [Description("Adds a user, channel, or role to the ignored xp list.")]
    public Task IgnoreAsync(CommandContext ctx,
        [Parameter("items")]
        [Description("The user, channel or role to ignore")]
        IReadOnlyList<SnowflakeObject> value) =>
        this.UpdateIgnoreState(ctx, value, true);

    [Command("Remove")]
    [Description("Removes a user, channel, or role from the ignored xp list.")]
    public Task WatchAsync(CommandContext ctx,
        [Parameter("Item")]
        [Description("The user, channel or role to remove from the ignore xp list.")]
        [VariadicArgument(10, 0)]
        IReadOnlyList<SnowflakeObject> value) =>
        this.UpdateIgnoreState(ctx, value, false);

    private async Task UpdateIgnoreState(CommandContext ctx, IReadOnlyList<SnowflakeObject> value, bool shouldIgnore)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var matchedIds = value.GroupBy(snowflakeObject => snowflakeObject.GetType().Name)
            .ToDictionary(group => group.Key,
                group => group.Select(snowflake => snowflake.Id));
        if (matchedIds.Count == 0 || (matchedIds.ContainsKey("Invalid") && matchedIds.Keys.Count == 1))
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "Could not parse any ids from the submitted values.");
            return;
        }

        IUpdateIgnoreForXpGain command = shouldIgnore
            ? new AddIgnoreForXpGain.Command { GuildId = ctx.Guild.Id }
            : new RemoveIgnoreForXpGain.Command { GuildId = ctx.Guild.Id };

        if (matchedIds.TryGetValue("DiscordUser", out var userIds))
            command.Users = await userIds
                .ToAsyncEnumerable()
                .SelectAwait(async x => await BuildUserDto(ctx.Client, x, ctx.Guild.Id))
                .OfType<UserDto>()
                .ToArrayAsync();

        if (matchedIds.TryGetValue("DiscordRole", out var roleIds))
            command.Roles = roleIds
                .Select(x =>
                    new RoleDto { Id = x, GuildId = ctx.Guild.Id }).ToArray();

        if (matchedIds.TryGetValue("DiscordChannel", out var channelIds))
            command.Channels = channelIds
                .Select(x =>
                    new ChannelDto { Id = x, GuildId = ctx.Guild.Id }).ToArray();
        var response = await this._mediator.Send(command);


        await ctx.EditReplyAsync(GrimoireColor.Green,
            string.IsNullOrWhiteSpace(response.Message)
                ? "All items in list provided were not ignored"
                : response.Message);
        await ctx.SendLogAsync(response, GrimoireColor.DarkPurple);
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
