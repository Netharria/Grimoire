// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Mute.Commands;

public partial class MuteAdminCommands
{
    [SlashCommand("View", "View the current configured mute role and any active mutes.")]
    public async Task ViewMutesAsync(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);
        var response = await this._mediator.Send(new GetAllActiveMutes.Query { GuildId = ctx.Guild.Id });

        DiscordRole? role = null;
        if (response.MuteRole is not null) role = ctx.Guild.Roles.GetValueOrDefault(response.MuteRole.Value);
        var users = ctx.Guild.Members.Where(x => response.MutedUsers.Contains(x.Key))
            .Select(x => x.Value).ToArray();
        var embed = new DiscordEmbedBuilder();

        embed.AddField("Mute Role", role?.Mention ?? "None");
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (users is not null && users.Length != 0)
            embed.AddField("Muted Users", string.Join(" ", users.Select(x => x.Mention)));
        else
            embed.AddField("Muted Users", "None");


        await ctx.EditReplyAsync(embed: embed);
    }
}

public sealed class GetAllActiveMutes
{
    public sealed record Query : IRequest<Response>
    {
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext)
        : IRequestHandler<Query, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<Response> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var result = await this._grimoireDbContext.GuildModerationSettings
                .AsNoTracking()
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new Response
                {
                    MuteRole = x.MuteRole, MutedUsers = x.Guild.ActiveMutes.Select(x => x.UserId).ToArray()
                }).FirstOrDefaultAsync(cancellationToken);
            if (result is null)
                throw new AnticipatedException("Could not find the settings for this server.");
            return result;
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong? MuteRole { get; init; }
        public ulong[] MutedUsers { get; init; } = [];
    }
}
