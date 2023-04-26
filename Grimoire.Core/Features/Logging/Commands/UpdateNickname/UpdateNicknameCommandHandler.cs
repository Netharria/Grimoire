// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Logging.Commands.UpdateNickname
{
    public class UpdateNicknameCommandHandler : ICommandHandler<UpdateNicknameCommand, UpdateNicknameCommandResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public UpdateNicknameCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<UpdateNicknameCommandResponse> Handle(UpdateNicknameCommand command, CancellationToken cancellationToken)
        {
            var currentNickname = await this._grimoireDbContext.NicknameHistory
                .WhereMemberHasId(command.UserId, command.GuildId)
                .Where(x => x.Guild.UserLogSettings.ModuleEnabled)
                .OrderByDescending(x => x.Timestamp)
                .Select(x => new
                {
                    x.Nickname,
                    x.Guild.UserLogSettings.NicknameChannelLogId
                })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (currentNickname is null
                || currentNickname.Nickname == command.Nickname)
                return new UpdateNicknameCommandResponse();

            await this._grimoireDbContext.NicknameHistory.AddAsync(
                new NicknameHistory
                {
                    GuildId = command.GuildId,
                    UserId = command.UserId,
                    Nickname = command.Nickname
                }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            if (currentNickname.NicknameChannelLogId is null)
                return new UpdateNicknameCommandResponse();
            return new UpdateNicknameCommandResponse
            {
                BeforeNickname = currentNickname.Nickname,
                AfterNickname = command.Nickname,
                NicknameChannelLogId = currentNickname.NicknameChannelLogId.Value
            };
        }
    }
}
