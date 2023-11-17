// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Moderation.Commands.MuteCommands.UnmuteUserCommand;

public class UnmuteUserCommandHandler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<UnmuteUserCommand, UnmuteUserCommandResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<UnmuteUserCommandResponse> Handle(UnmuteUserCommand command, CancellationToken cancellationToken)
    {
        var response = await this._grimoireDbContext.Mutes
            .WhereMemberHasId(command.UserId, command.GuildId)
            .Select(x => new
            {
                Mute = x,
                x.Guild.ModerationSettings.MuteRole,
                x.Guild.ModChannelLog
            }).FirstOrDefaultAsync(cancellationToken);
        if (response is null) throw new AnticipatedException("That user doesn't seem to be muted.");
        if (response.MuteRole is null) throw new AnticipatedException("A mute role isn't currently configured.");
        this._grimoireDbContext.Mutes.Remove(response.Mute);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        return new UnmuteUserCommandResponse
        {
            MuteRole = response.MuteRole.Value,
            LogChannelId = response.ModChannelLog
        };
    }
}
