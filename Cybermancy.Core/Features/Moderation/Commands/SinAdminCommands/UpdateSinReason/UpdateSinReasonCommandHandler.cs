// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Exceptions;
using Cybermancy.Core.Extensions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Moderation.Commands.SinAdminCommands.UpdateSinReason
{
    public class UpdateSinReasonCommandHandler : ICommandHandler<UpdateSinReasonCommand, UpdateSinReasonCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public UpdateSinReasonCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<UpdateSinReasonCommandResponse> Handle(UpdateSinReasonCommand command, CancellationToken cancellationToken)
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

            result.Sin.Reason = command.Reason;

            this._cybermancyDbContext.Sins.Update(result.Sin);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return new UpdateSinReasonCommandResponse
            {
                SinId = command.SinId,
                SinnerName = result.UserName ?? UserExtensions.Mention(result.Sin.UserId),
                LogChannelId = result.ModChannelLog
            };
        }
    }
}
