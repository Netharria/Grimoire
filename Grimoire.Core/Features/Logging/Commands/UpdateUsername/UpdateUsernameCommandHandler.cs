// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Logging.Commands.UpdateUsername
{
    public class UpdateUsernameCommandHandler : ICommandHandler<UpdateUsernameCommand, UpdateUsernameCommandResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public UpdateUsernameCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<UpdateUsernameCommandResponse> Handle(UpdateUsernameCommand command, CancellationToken cancellationToken)
        {
            var currentUsername = await this._grimoireDbContext.Members
                .WhereMemberHasId(command.UserId, command.GuildId)
                .Where(x => x.Guild.UserLogSettings.ModuleEnabled)
                .Select(x => new
                {
                    x.User.UsernameHistories.OrderByDescending(x => x.Timestamp).First().Username,
                    x.Guild.UserLogSettings.UsernameChannelLogId
                }).FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (currentUsername is null
                || currentUsername.Username == command.Username)
                return new UpdateUsernameCommandResponse();

            await this._grimoireDbContext.UsernameHistory.AddAsync(
                new UsernameHistory
                {
                    UserId = command.UserId,
                    Username = command.Username
                }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            if (currentUsername.UsernameChannelLogId is null)
                return new UpdateUsernameCommandResponse();
            return new UpdateUsernameCommandResponse
            {
                BeforeUsername = currentUsername.Username,
                AfterUsername = command.Username,
                UsernameChannelLogId = currentUsername.UsernameChannelLogId.Value
            };
        }
    }
}
