// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Commands;

public sealed class AwardUserXp
{
    public sealed record Command : ICommand<BaseResponse>
    {
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
        public required long XpToAward { get; init; }
        public ulong? AwarderId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : ICommandHandler<Command, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {

            var member = await this._grimoireDbContext.Members
            .AsNoTracking()
            .WhereMemberHasId(command.UserId, command.GuildId)
            .Select(x => new
            {
                x.Guild.ModChannelLog
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (member is null)
                throw new AnticipatedException($"{UserExtensions.Mention(command.UserId)} was not found. Have they been on the server before?");

            await this._grimoireDbContext.XpHistory.AddAsync(new XpHistory
            {
                GuildId = command.GuildId,
                UserId = command.UserId,
                Xp = command.XpToAward,
                TimeOut = DateTimeOffset.UtcNow,
                Type = XpHistoryType.Awarded,
                AwarderId = command.AwarderId,
            }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse()
            {
                LogChannelId = member.ModChannelLog
            };
        }
    }

}

