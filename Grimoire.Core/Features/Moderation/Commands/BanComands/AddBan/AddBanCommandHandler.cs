// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Moderation.Commands.BanComands.AddBan
{
    public class AddBanCommandHandler : ICommandHandler<AddBanCommand, AddBanCommandResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public AddBanCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<AddBanCommandResponse> Handle(AddBanCommand command, CancellationToken cancellationToken)
        {
            var sin = await this._grimoireDbContext.Sins.AddAsync(new Sin
            {
                GuildId = command.GuildId,
                UserId = command.UserId,
                Reason = command.Reason,
                SinType = SinType.Ban,
                ModeratorId = command.ModeratorId
            }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            var loggingChannel = await this._grimoireDbContext.Guilds
                .WhereIdIs(command.GuildId)
                .Select(x => x.ModChannelLog)
                .FirstOrDefaultAsync(cancellationToken);
            return new AddBanCommandResponse { SinId = sin.Entity.Id, LogChannelId = loggingChannel };
        }
    }
}
