// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Moderation.Commands.SinAdminCommands.PardonSin;

public class PardonSinCommandHandler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<PardonSinCommand, PardonSinCommandResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<PardonSinCommandResponse> Handle(PardonSinCommand command, CancellationToken cancellationToken)
    {
        var result = await this._grimoireDbContext.Sins
            .Where(x => x.Id == command.SinId)
            .Where(x => x.GuildId == command.GuildId)
            .Include(x => x.Pardon)
            .Select(x => new
            {
                Sin = x,
                UserName = x.Member.User.UsernameHistories
                .OrderByDescending(x => x.Id)
                .Select(x => x.Username)
                .FirstOrDefault(),
                x.Guild.ModChannelLog
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
            throw new AnticipatedException("Could not find a sin with that ID.");

        if (result.Sin.Pardon is not null)
        {
            result.Sin.Pardon.Reason = command.Reason;
        }
        else
        {
            result.Sin.Pardon = new Pardon
            {
                SinId = command.SinId,
                GuildId = command.GuildId,
                ModeratorId = command.ModeratorId,
                Reason = command.Reason,
            };
        }
        this._grimoireDbContext.Sins.Update(result.Sin);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        return new PardonSinCommandResponse
        {
            SinId = command.SinId,
            SinnerName = result.UserName ?? UserExtensions.Mention(result.Sin.UserId),
            LogChannelId = result.ModChannelLog
        };
    }
}
