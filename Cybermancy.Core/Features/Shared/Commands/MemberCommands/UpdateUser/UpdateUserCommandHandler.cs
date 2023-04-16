// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Shared.Commands.MemberCommands.UpdateUser
{
    public class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public UpdateUserCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<Unit> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
        {
            var userName = await this._cybermancyDbContext.UsernameHistory
                .Where(x => x.UserId == command.UserId)
                .OrderByDescending(x => x.Timestamp)
                .Select(x => x.Username)
                .FirstAsync(cancellationToken: cancellationToken);
            if (userName.Equals(command.UserName, StringComparison.Ordinal))
                return Unit.Value;
            await this._cybermancyDbContext.UsernameHistory.AddAsync(new UsernameHistory
                {
                    UserId = command.UserId,
                    Username = command.UserName
                }, cancellationToken);

            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}
