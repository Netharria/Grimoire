// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Commands;

public enum XpOption
{
    All,
    Amount
}

public sealed class ReclaimUserXp
{
    public sealed record Command : ICommand<Response>
    {
        public XpOption XpOption { get; init; }
        public long XpToTake { get; init; }
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
        public ulong? ReclaimerId { get; init; }
    }

    public sealed class Handler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<Command, Response>
    {
        private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response> Handle(Command command, CancellationToken cancellationToken)
        {
            var member = await this._grimoireDbContext.Members
            .AsNoTracking()
            .WhereMemberHasId(command.UserId, command.GuildId)
            .Select(x => new
            {
                Xp = x.XpHistory.Sum(x => x.Xp ),
                x.Guild.ModChannelLog
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (member is null)
                throw new AnticipatedException($"{UserExtensions.Mention(command.UserId)} was not found. Have they been on the server before?");

            var xpToTake = command.XpOption switch
            {
                XpOption.All => member.Xp,
                XpOption.Amount => command.XpToTake,
                _ => throw new ArgumentOutOfRangeException(nameof(command),"XpOption not implemented in switch statement.")
            };
            if (member.Xp < xpToTake)
                xpToTake = member.Xp;
            await this._grimoireDbContext.XpHistory.AddAsync(new XpHistory
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                Xp = -xpToTake,
                Type = XpHistoryType.Reclaimed,
                AwarderId = command.ReclaimerId,
                TimeOut = DateTimeOffset.UtcNow
            }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            return new Response
            {
                LogChannelId = member.ModChannelLog,
                XpTaken = xpToTake
            };
        }
    }

    public sealed record Response : BaseResponse
    {
        public long XpTaken { get; init; }
    }

}

