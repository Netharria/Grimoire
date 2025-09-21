// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;

namespace Grimoire.Features.Leveling.Settings;

public sealed partial class IgnoreCommandGroup
{
    [Command("View")]
    [Description("Displays all ignored users, channels and roles on this server.")]
    public async Task ShowIgnoredAsync(SlashCommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var response = await this._mediator.Send(new GetIgnoredItems.Query { GuildId = ctx.Guild.Id });


        if (!response.IgnoredRoles.Any() && !response.IgnoredChannels.Any() &&
            !response.IgnoredMembers.Any())
            throw new AnticipatedException("This server does not have any ignored channels, roles or users.");


        var embed = new DiscordEmbedBuilder()
            .WithTitle("Ignored Channels Roles and Users.")
            .WithTimestamp(DateTime.UtcNow);
        var embedPages = InteractivityExtension.GeneratePagesInEmbed(
            await BuildMessageAsync(ctx, response),
            SplitType.Line,
            embed);
        await ctx.Interaction.SendPaginatedResponseAsync(false, ctx.User, embedPages);
    }

    private static async Task<string> BuildMessageAsync(SlashCommandContext ctx, GetIgnoredItems.Response response)
    {
        ArgumentNullException.ThrowIfNull(ctx.Guild);
        var ignoredMessageBuilder = new StringBuilder().Append("**Channels**\n");
        foreach (var channel in response.IgnoredChannels)
        {
            var discordChannel = await ctx.Guild.GetChannelOrDefaultAsync(channel);
            if (discordChannel is null)
                continue;
            ignoredMessageBuilder.Append(discordChannel.Mention).Append('\n');
        }


        ignoredMessageBuilder.Append("\n**Roles**\n");

        foreach (var role in response.IgnoredRoles)
        {
            var discordRole = await ctx.Guild.GetRoleOrDefaultAsync(role);
            if (discordRole is null)
                continue;
            ignoredMessageBuilder.Append(discordRole.Mention).Append('\n');
        }

        ignoredMessageBuilder.Append("\n**Users**\n");

        foreach (var member in response.IgnoredMembers)
        {
            var user = await ctx.Client.GetUserOrDefaultAsync(member);
            if (user is null)
                continue;
            ignoredMessageBuilder.Append(user.Mention).Append('\n');
        }

        return ignoredMessageBuilder.ToString();
    }
}

public sealed class GetIgnoredItems
{
    public sealed record Query : IRequest<Response>
    {
        public GuildId GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Query request, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var ignoredItems = await dbContext.Guilds
                .AsNoTracking()
                .AsSplitQuery()
                .WhereIdIs(request.GuildId)
                .Select(guild => new Response
                {
                    IgnoredRoles = guild.IgnoredRoles.Select(ignoredRole => ignoredRole.RoleId),
                    IgnoredChannels = guild.IgnoredChannels.Select(ignoredChannel => ignoredChannel.ChannelId),
                    IgnoredMembers = guild.IgnoredMembers.Select(ignoredMember => ignoredMember.UserId)
                }).FirstOrDefaultAsync(cancellationToken);


            if (ignoredItems is null)
                throw new AnticipatedException("Could not find the settings for this server.");


            return ignoredItems;
        }
    }

    public sealed record Response
    {
        public required IEnumerable<ulong> IgnoredRoles { get; init; }
        public required IEnumerable<ulong> IgnoredChannels { get; init; }
        public required IEnumerable<ulong> IgnoredMembers { get; init; }
    }
}
