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
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
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
                await this.AddMissingMember(dbContext, command, cancellationToken);
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

            var loggingChannel = await dbContext.Guilds
                .WhereIdIs(command.GuildId)
                .Select(x => x.ModChannelLog)
                .FirstOrDefaultAsync(cancellationToken);
            return new Response { SinId = sin.Entity.Id, LogChannelId = loggingChannel };
        }

        private async Task AddMissingMember(GrimoireDbContext dbContext, Command command,
            CancellationToken cancellationToken)
        {
            if (!await dbContext.Users.AnyAsync(x => x.Id == command.UserId, cancellationToken))
                await dbContext.Users.AddAsync(new User { Id = command.UserId }, cancellationToken);
            await dbContext.Members.AddAsync(new Member
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                XpHistory =
                [
                    new XpHistory
                    {
                        UserId = command.UserId,
                        GuildId = command.GuildId,
                        Xp = 0,
                        Type = XpHistoryType.Created,
                        TimeOut = DateTime.UtcNow
                    }
                ]
            }, cancellationToken);
        }
    }

    public sealed record Response : BaseResponse
    {
        public long SinId { get; init; }
    }
}