// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.AddLogMessage;

public class AddLogMessageCommandHandler : ICommandHandler<AddLogMessageCommand>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public AddLogMessageCommandHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<Unit> Handle(AddLogMessageCommand command, CancellationToken cancellationToken)
    {
        var logMessage = new OldLogMessage
        {
            ChannelId = command.ChannelId,
            GuildId = command.GuildId,
            Id = command.MessageId
        };
        await this._grimoireDbContext.OldLogMessages.AddAsync(logMessage, cancellationToken);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
