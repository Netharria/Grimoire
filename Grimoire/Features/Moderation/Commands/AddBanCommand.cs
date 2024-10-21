// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Moderation.Commands;

public sealed class AddBan
{
    public sealed record Command : IRequest<Response>
    {
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
        public string Reason { get; set; } = string.Empty;
        public ulong? ModeratorId { get; set; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Command, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<Response> Handle(Command command, CancellationToken cancellationToken)
        {
            var memberExists = await this._grimoireDbContext.Members
                .AnyAsync(x => x.UserId == command.UserId
                 && x.GuildId == command.GuildId, cancellationToken);
            if (!memberExists)
                await this.AddMissingMember(command, cancellationToken);
            var sin = await this._grimoireDbContext.Sins.AddAsync(new Sin
            {
                GuildId = command.GuildId,
                UserId = command.UserId,
                Reason = command.Reason,
                SinType = SinType.Ban,
                ModeratorId = command.ModeratorId
            }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            var loggingChannel = await this._grimoireDbContext.Guilds
            .WhereIdIs(command.GuildId)
            .Select(x => x.ModChannelLog)
            .FirstOrDefaultAsync(cancellationToken);
            return new Response { SinId = sin.Entity.Id, LogChannelId = loggingChannel };
        }

        private async Task AddMissingMember(Command command, CancellationToken cancellationToken)
        {
            if (!await this._grimoireDbContext.Users.AnyAsync(x => x.Id == command.UserId, cancellationToken))
                await this._grimoireDbContext.Users.AddAsync(new User { Id = command.UserId }, cancellationToken);
            await this._grimoireDbContext.Members.AddAsync(new Member
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                XpHistory =
                    [
                        new() {
                            UserId = command.UserId,
                            GuildId = command.GuildId,
                            Xp = 0,
                            Type = XpHistoryType.Created,
                            TimeOut = DateTime.UtcNow
                        }
                    ],
            }, cancellationToken);
        }
    }

    public sealed record Response : BaseResponse
    {
        public long SinId { get; init; }
    }

}

