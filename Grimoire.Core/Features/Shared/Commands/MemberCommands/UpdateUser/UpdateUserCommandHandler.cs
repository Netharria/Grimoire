// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Shared.Commands.MemberCommands.UpdateUser;

public class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public UpdateUserCommandHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<Unit> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var userName = await this._grimoireDbContext.UsernameHistory
            .AsNoTracking()
            .Where(x => x.UserId == command.UserId)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => x.Username)
            .FirstAsync(cancellationToken: cancellationToken);
        if (userName.Equals(command.UserName, StringComparison.Ordinal))
            return Unit.Value;
        await this._grimoireDbContext.UsernameHistory.AddAsync(new UsernameHistory
        {
            UserId = command.UserId,
            Username = command.UserName
        }, cancellationToken);

        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
