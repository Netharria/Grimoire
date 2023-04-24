// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Moderation.Commands.WarnCommands
{
    public class WarnUserCommandHandler : ICommandHandler<WarnUserCommand, WarnUserCommandResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public WarnUserCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<WarnUserCommandResponse> Handle(WarnUserCommand command, CancellationToken cancellationToken)
        {
            var sin = new Sin
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                ModeratorId = command.ModeratorId,
                Reason = command.Reason,
                SinType = SinType.Warn
            };
            await this._grimoireDbContext.Sins
                .AddAsync(sin, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            var LogChannelId = await this._grimoireDbContext.Guilds
                .WhereIdIs(command.GuildId)
                .Select(x => x.ModChannelLog).FirstOrDefaultAsync(cancellationToken);
            return new WarnUserCommandResponse
            {
                SinId = sin.Id,
                LogChannelId = LogChannelId
            };
        }
    }
}
