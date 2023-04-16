// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Extensions;

namespace Cybermancy.Core.Features.Moderation.Commands.SinAdminCommands.ForgetSin
{
    public class ForgetSinCommandHandler : ICommandHandler<ForgetSinCommand, ForgetSinCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public ForgetSinCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<ForgetSinCommandResponse> Handle(ForgetSinCommand command, CancellationToken cancellationToken)
        {
            var result = await this._cybermancyDbContext.Sins
                .Where(x => x.Id == command.SinId)
                .Where(x => x.GuildId == command.GuildId)
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

            if (result is null) throw new AnticipatedException("Could not find a sin with that ID.");


            this._cybermancyDbContext.Sins.Remove(result.Sin);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return new ForgetSinCommandResponse
            {
                SinId = command.SinId,
                SinnerName = result.UserName ?? UserExtensions.Mention(result.Sin.UserId),
                LogChannelId = result.ModChannelLog
            };
        }
    }
}
