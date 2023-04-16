// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Commands.SetBanLogChannel
{
    public class SetBanLogChannelCommandHandler : ICommandHandler<SetBanLogChannelCommand>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public SetBanLogChannelCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<Unit> Handle(SetBanLogChannelCommand command, CancellationToken cancellationToken)
        {
            var guildModerationSettings = await this._grimoireDbContext.GuildModerationSettings
                .FirstOrDefaultAsync(x => x.GuildId.Equals(command.GuildId), cancellationToken: cancellationToken);
            if (guildModerationSettings is null) throw new AnticipatedException("Could not find the Servers settings.");

            guildModerationSettings.PublicBanLog = command.ChannelId;
            this._grimoireDbContext.GuildModerationSettings.Update(guildModerationSettings);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            return new Unit();
        }
    }
}
