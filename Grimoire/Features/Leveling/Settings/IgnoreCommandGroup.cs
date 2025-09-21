// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.Leveling.Settings;

[RequireGuild]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
[RequireModuleEnabled(Module.Leveling)]
[Command("Ignore")]
[Description("Commands for updating and viewing the server ignore list.")]
public sealed partial class IgnoreCommandGroup(IMediator mediator, GuildLog guildLog)
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly IMediator _mediator = mediator;

    [Command("Add")]
    [Description("Adds a user, channel, or role to the ignored xp list.")]
    public Task IgnoreAsync(CommandContext ctx,
        [Parameter("items")] [Description("The user, channel or role to ignore")]
        params SnowflakeObject[] value) =>
        UpdateIgnoreState(ctx, value, true);

    [Command("Remove")]
    [Description("Removes a user, channel, or role from the ignored xp list.")]
    public Task WatchAsync(CommandContext ctx,
        [Parameter("Item")] [Description("The user, channel or role to remove from the ignore xp list.")]
        params SnowflakeObject[] value) =>
        UpdateIgnoreState(ctx, value, false);

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

        var messageBuilder = await BuildIgnoreListAsync(ctx, response)
            ;
        var message = messageBuilder.Length == 0
            ? "All items in list provided were not ignored"
            : messageBuilder
                .Append(shouldIgnore ? " are now ignored for xp gain." : " are no longer ignored for xp gain.")
                .ToString();

        await ctx.EditReplyAsync(GrimoireColor.Green,
            message);
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = message
        });
    }

    private static async ValueTask<UserDto?> BuildUserDto(DiscordClient client, ulong id, ulong guildId)
    {
        if (client.Guilds[guildId].Members.TryGetValue(id, out var member))
            return new UserDto
            {
                Id = member.Id,
                Nickname = member.Nickname,
                Username = member.Username,
                AvatarUrl = member.GetGuildAvatarUrl(MediaFormat.Auto)
            };
        try
        {
            var user = await client.GetUserAsync(id);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (user is not null)
                return new UserDto
                {
                    Id = user.Id, Username = user.Username, AvatarUrl = user.GetAvatarUrl(MediaFormat.Auto)
                };
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    private static async Task<StringBuilder> BuildIgnoreListAsync(CommandContext ctx, Response response)
    {
        ArgumentNullException.ThrowIfNull(ctx.Guild);
        var stringBuilder = new StringBuilder();
        if (response.IgnoredMembers is not null)
            foreach (var ignorable in response.IgnoredMembers)
            {
                var ignoredMember = await ctx.Client.GetUserOrDefaultAsync(ignorable.UserId);
                if (ignoredMember is not null)
                    stringBuilder.Append(ignoredMember.Mention).Append(' ');
            }

        if (response.IgnoredRoles is not null)
            foreach (var ignorable in response.IgnoredRoles)
            {
                var ignoredRole = await ctx.Guild.GetRoleOrDefaultAsync(ignorable.RoleId);
                if (ignoredRole is not null)
                    stringBuilder.Append(ignoredRole.Mention).Append(' ');
            }

        if (response.IgnoredChannels is not null)
            foreach (var ignorable in response.IgnoredChannels)
            {
                var ignoredChannel = await ctx.Guild.GetChannelOrDefaultAsync(ignorable.ChannelId);
                if (ignoredChannel is not null)
                    stringBuilder.Append(ignoredChannel.Mention).Append(' ');
            }

        return stringBuilder;
    }

    public interface IUpdateIgnoreForXpGain : IRequest<Response>
    {
        public GuildId GuildId { get; init; }
        public IReadOnlyCollection<UserDto> Users { get; set; }
        public IReadOnlyCollection<RoleDto> Roles { get; set; }
        public IReadOnlyCollection<ChannelDto> Channels { get; set; }
    }

    public sealed record Response
    {
        public IgnoredChannel[]? IgnoredChannels { get; internal set; }
        public IgnoredMember[]? IgnoredMembers { get; internal set; }
        public IgnoredRole[]? IgnoredRoles { get; internal set; }
    }
}
