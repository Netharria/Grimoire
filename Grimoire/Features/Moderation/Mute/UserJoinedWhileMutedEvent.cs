// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;
using JetBrains.Annotations;

namespace Grimoire.Features.Moderation.Mute;

public sealed class UserJoinedWhileMuted
{
    public sealed class EventHandler(IMediator mediator) : IEventHandler<GuildMemberAddedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, GuildMemberAddedEventArgs args)
        {
            var response = await this._mediator.Send(new Query { UserId = args.Member.Id, GuildId = args.Guild.Id });
            if (response is null) return;
            var role = args.Guild.Roles.GetValueOrDefault(response.Value);
            if (role is null) return;
            await args.Member.GrantRoleAsync(role, "Rejoined while muted");
        }
    }

    public sealed record Query : IRequest<ulong?>
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
    }

    [UsedImplicitly]
    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, ulong?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<ulong?> Handle(Query query, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Mutes
                .AsNoTracking()
                .WhereMemberHasId(query.UserId, query.GuildId)
                .Where(x => x.Guild.ModerationSettings.ModuleEnabled)
                .Select(x =>
                    x.Guild.ModerationSettings.MuteRole)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
