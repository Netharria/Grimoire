// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Extensions;

namespace Cybermancy.Core.Features.Moderation.Commands.SinAdminCommands.PardonSin
{
    public class PardonSinCommandHandler : ICommandHandler<PardonSinCommand, PardonSinCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public PardonSinCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<PardonSinCommandResponse> Handle(PardonSinCommand command, CancellationToken cancellationToken)
        {
            var result = await this._cybermancyDbContext.Sins
                .Where(x => x.Id == command.SinId)
                .Where(x => x.GuildId == command.GuildId)
                .Include(x => x.Pardon)
                .Select(x => new
                {
                    Sin = x,
                    UserName = x.Member.User.UsernameHistories
                    .OrderByDescending(x => x.Id)
                    .Select(x => x.Username)
                    .FirstOrDefault(),
                    x.Guild.ModChannelLog
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (result is null)
                throw new AnticipatedException("Could not find a sin with that ID.");

            if(result.Sin.Pardon is not null)
            {
                result.Sin.Pardon.Reason = command.Reason;
            }
            else
            {
                result.Sin.Pardon = new Pardon
                {
                    SinId = command.SinId,
                    GuildId = command.GuildId,
                    ModeratorId = command.ModeratorId,
                    Reason = command.Reason,
                };
            }
            this._cybermancyDbContext.Sins.Update(result.Sin);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return new PardonSinCommandResponse
            {
                SinId = command.SinId,
                SinnerName = result.UserName ?? UserExtensions.Mention(result.Sin.UserId),
                LogChannelId = result.ModChannelLog
            };
        }
    }
}
