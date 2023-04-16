// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Moderation.Commands.MuteCommands.SetMuteRole
{
    public class SetMuteRoleCommandHandler : ICommandHandler<SetMuteRoleCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public SetMuteRoleCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<Unit> Handle(SetMuteRoleCommand command, CancellationToken cancellationToken)
        {
            var guildModerationSettings = await this._cybermancyDbContext.GuildModerationSettings
                .FirstOrDefaultAsync(x => x.GuildId == command.GuildId, cancellationToken);
            if (guildModerationSettings is null) throw new AnticipatedException("Could not find the Servers settings.");

            guildModerationSettings.MuteRole = command.Role;
            this._cybermancyDbContext.GuildModerationSettings.Update(guildModerationSettings);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return new Unit();
        }
    }
}
