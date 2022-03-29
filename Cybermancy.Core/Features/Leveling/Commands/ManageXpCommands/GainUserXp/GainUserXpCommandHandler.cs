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
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Leveling.Commands.ManageXpCommands.GainUserXp
{
    public class GainUserXpCommandHandler : IRequestHandler<GainUserXpCommand, GainUserXpCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GainUserXpCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<GainUserXpCommandResponse> Handle(GainUserXpCommand request, CancellationToken cancellationToken)
        {
            var member = await this._cybermancyDbContext.Members
                .WhereMemberHasId(request.UserId, request.GuildId)
                .WhereLevelingEnabled()
                .WhereMemberNotIgnored(request.ChannelId, request.RoleIds)
                .Select(x => new
                {
                    x.Xp,
                    x.TimeOut,
                    x.Guild.LevelSettings.Base,
                    x.Guild.LevelSettings.Modifier,
                    x.Guild.LevelSettings.Amount,
                    x.Guild.LevelSettings.LevelChannelLogId,
                    x.Guild.LevelSettings.TextTime
                }).FirstOrDefaultAsync(cancellationToken);

            if (member is null || member.TimeOut > DateTime.UtcNow)
                return new GainUserXpCommandResponse { Success = false };

            var previousLevel = MemberExtensions.GetLevel(member.Xp, member.Base, member.Modifier);
            var currentLevel = MemberExtensions.GetLevel(member.Xp + member.Amount, member.Base, member.Modifier);

            await this._cybermancyDbContext.UpdateItemPropertiesAsync(
                new Member
                {
                    UserId = request.UserId,
                    GuildId = request.GuildId,
                    Xp = member.Xp + member.Amount,
                    TimeOut = DateTime.UtcNow + member.TextTime
                },
                x => x.Xp,
                x => x.TimeOut);

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
                LoggingChannel = member.LevelChannelLogId
            };

        }
    }
}
