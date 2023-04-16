// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Shared.Commands.ModuleCommands.EnableModuleCommand
{
    public class EnableModuleCommandHandler : ICommandHandler<EnableModuleCommand, EnableModuleCommandResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public EnableModuleCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<EnableModuleCommandResponse> Handle(EnableModuleCommand command, CancellationToken cancellationToken)
        {
            var guildModule = await this._grimoireDbContext.Guilds
                .WhereIdIs(command.GuildId)
                .GetModulesOfType(command.Module, cancellationToken)
                .Select(x => new
                {
                    Module = x,
                    x.Guild.ModChannelLog,
                })
                .FirstAsync(cancellationToken);
            guildModule.Module.ModuleEnabled = command.Enable;
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new EnableModuleCommandResponse
            {
                ModerationLog = guildModule.ModChannelLog
            };
        }
    }
}
