// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.MessageLoggingCommands;

public sealed record DeleteOldMessagesCommand : ICommand
{
}

public class DeleteOldMessagesCommandHandler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<DeleteOldMessagesCommand>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<Unit> Handle(DeleteOldMessagesCommand command, CancellationToken cancellationToken)
    {
        var oldDate = DateTimeOffset.UtcNow - TimeSpan.FromDays(31);
        await this._grimoireDbContext.Messages
            .Where(x => x.CreatedTimestamp <= oldDate)
            .ExecuteDeleteAsync(cancellationToken);
        return Unit.Value;
    }
}
