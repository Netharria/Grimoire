// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Shared.Commands.MemberCommands.UpdateMember
{
    public class UpdateMemberCommandHandler : ICommandHandler<UpdateMemberCommand>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public UpdateMemberCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<Unit> Handle(UpdateMemberCommand command, CancellationToken cancellationToken)
        {
            var currentNickname = await this._grimoireDbContext.NicknameHistory
                .WhereMemberHasId(command.UserId, command.GuildId)
                .OrderByDescending(x => x.Timestamp)
                .Select(x => x.Nickname)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(command.Nickname)
                || string.IsNullOrWhiteSpace(currentNickname)
                || currentNickname.Equals(command.Nickname, StringComparison.Ordinal))
                return Unit.Value;
            await this._grimoireDbContext.NicknameHistory.AddAsync(
                new NicknameHistory
                {
                    GuildId = command.GuildId,
                    UserId = command.UserId,
                    Nickname = command.Nickname
                }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}
