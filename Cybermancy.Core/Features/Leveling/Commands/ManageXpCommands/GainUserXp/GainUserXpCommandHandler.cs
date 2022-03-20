// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Extensions;
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
            if (await this._cybermancyDbContext.Guilds
                .Where(x => x.Id == request.GuildId)
                .AnyAsync(x =>
                x.Roles.Where(x => request.RoleIds.Contains(x.Id)).Any(y => y.IsXpIgnored) ||
                x.Channels.Where(x => x.Id == request.ChannelId).Any(y => y.IsXpIgnored) ||
                x.GuildUsers.Where(x => x.UserId == request.UserId).Any(y => y.IsXpIgnored) ||
                !x.LevelSettings.IsLevelingEnabled, cancellationToken: cancellationToken))
                return new GainUserXpCommandResponse { Success = false };

            var user = await this._cybermancyDbContext.GuildUsers
                .FirstOrDefaultAsync(x => x.UserId == request.UserId, cancellationToken: cancellationToken);

            if (user is null || user.TimeOut > DateTime.UtcNow)
                return new GainUserXpCommandResponse { Success = false };

            var previousLevel = user.GetLevel(this._cybermancyDbContext);
            user.GrantXp(this._cybermancyDbContext);
            var currentLevel = user.GetLevel(this._cybermancyDbContext);

            this._cybermancyDbContext.GuildUsers.Update(user);

            var result = await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            var earnedRewards = await this._cybermancyDbContext.Rewards
                .Where(x => x.GuildId == request.GuildId)
                .Where(x => x.RewardLevel <= currentLevel)
                .Select(x => x.RoleId )
                .ToArrayAsync(cancellationToken: cancellationToken);

            var loggingChannel = await this._cybermancyDbContext.GuildLevelSettings
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => x.LevelChannelLogId)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            return new GainUserXpCommandResponse
            {
                Success = true,
                EarnedRewards = earnedRewards,
                PreviousLevel = previousLevel,
                CurrentLevel = currentLevel,
                LoggingChannel = loggingChannel
            };

        }
    }
}
