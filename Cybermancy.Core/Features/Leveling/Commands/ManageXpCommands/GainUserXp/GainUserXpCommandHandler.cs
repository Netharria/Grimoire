// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Extensions;
using Cybermancy.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Leveling.Commands.ManageXpCommands.GainUserXp
{
    public class GainUserXpCommandHandler : ICommandHandler<GainUserXpCommand, GainUserXpCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GainUserXpCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<GainUserXpCommandResponse> Handle(GainUserXpCommand request, CancellationToken cancellationToken)
        {
            var result = await this._cybermancyDbContext.Members
                .WhereMemberHasId(request.UserId, request.GuildId)
                .WhereLevelingEnabled()
                .WhereMemberNotIgnored(request.ChannelId, request.RoleIds)
                .Select(x => new
                {
                    Xp = x.XpHistory.Sum(x => x.Xp),
                    x.XpHistory.OrderByDescending(x => x.TimeOut).First().TimeOut,
                    x.Guild.LevelSettings.Base,
                    x.Guild.LevelSettings.Modifier,
                    x.Guild.LevelSettings.Amount,
                    x.Guild.LevelSettings.LevelChannelLogId,
                    x.Guild.LevelSettings.TextTime
                }).FirstOrDefaultAsync(cancellationToken);

            if (result is null || result.TimeOut > DateTime.UtcNow)

                return new GainUserXpCommandResponse { Success = false };

            var previousLevel = MemberExtensions.GetLevel(result.Xp, result.Base, result.Modifier);
            var currentLevel = MemberExtensions.GetLevel(result.Xp + result.Amount, result.Base, result.Modifier);

            await this._cybermancyDbContext.XpHistory.AddAsync(new XpHistory
                {
                    Xp = result.Amount,
                    UserId = request.UserId,
                    GuildId = request.GuildId,
                    TimeOut = DateTimeOffset.UtcNow + result.TextTime,
                    Type = XpHistoryType.Earned
                }, cancellationToken);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            var earnedRewards = await this._cybermancyDbContext.Rewards
                .Where(x => x.GuildId == request.GuildId)
                .Where(x => x.RewardLevel <= currentLevel)
                .Select(x => x.RoleId )
                .ToArrayAsync(cancellationToken: cancellationToken);

            return new GainUserXpCommandResponse
            {
                Success = true,
                EarnedRewards = earnedRewards,
                PreviousLevel = previousLevel,
                CurrentLevel = currentLevel,
                LoggingChannel = result.LevelChannelLogId
            };

        }
    }
}
