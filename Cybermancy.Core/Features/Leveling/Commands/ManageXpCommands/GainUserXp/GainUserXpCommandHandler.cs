// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
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
            var user = await this._cybermancyDbContext.GuildUsers
                .Where(x => x.UserId == request.UserId
                && x.GuildId == request.GuildId)
                .Where(x => x.Guild.LevelSettings.IsLevelingEnabled)
                .Where(x => !x.IsXpIgnored)
                .Where(x => !x.Guild.Roles.Where(x => request.RoleIds.Contains(x.Id)).Any(y => y.IsXpIgnored)
                || !x.Guild.Channels.Where(x => x.Id == request.ChannelId).Any(y => y.IsXpIgnored))
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

            if (user is null || user.TimeOut > DateTime.UtcNow)
                return new GainUserXpCommandResponse { Success = false };

            var previousLevel = GuildUserExtensions.GetLevel(user.Xp, user.Base, user.Modifier);
            var currentLevel = GuildUserExtensions.GetLevel(user.Xp + user.Amount, user.Base, user.Modifier);

            await this._cybermancyDbContext.UpdateItemPropertiesAsync(
                new GuildUser
                {
                    UserId = request.UserId,
                    GuildId = request.GuildId,
                    Xp = user.Xp + user.Amount,
                    TimeOut = DateTime.UtcNow + TimeSpan.FromMinutes(user.TextTime)
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
                LoggingChannel = user.LevelChannelLogId
            };

        }
    }
}
