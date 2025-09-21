// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Moderation.Ban.Shared;

public sealed class AddBan
{
    public sealed record Command : IRequest<Response>
    {
        public required UserId UserId { get; init; }
        public required GuildId GuildId { get; init; }
        public string Reason { get; set; } = string.Empty;
        public ulong? ModeratorId { get; set; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var memberExists = await dbContext.Members
                .AnyAsync(x => x.UserId == command.UserId
                               && x.GuildId == command.GuildId, cancellationToken);
            if (!memberExists)
                await dbContext.AddMissingMember(command.UserId, command.GuildId, cancellationToken);
            var sin = await dbContext.Sins.AddAsync(
                new Sin
                {
                    GuildId = command.GuildId,
                    UserId = command.UserId,
                    Reason = command.Reason,
                    SinType = SinType.Ban,
                    ModeratorId = command.ModeratorId
                }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response { SinId = sin.Entity.Id };
        }
    }

    public sealed record Response
    {
        public long SinId { get; init; }
    }
}
