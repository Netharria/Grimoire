// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Mute;

public sealed record SetMuteRoleCommand : IRequest<BaseResponse>
{
    public ulong Role { get; init; }
    public ulong GuildId { get; init; }
}

public sealed class SetMuteRoleCommandHandler(GrimoireDbContext grimoireDbContext)
    : IRequestHandler<SetMuteRoleCommand, BaseResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async Task<BaseResponse> Handle(SetMuteRoleCommand command, CancellationToken cancellationToken)
    {
        var guildModerationSettings = await this._grimoireDbContext.GuildModerationSettings
            .Include(x => x.Guild)
            .FirstOrDefaultAsync(x => x.GuildId == command.GuildId, cancellationToken);
        if (guildModerationSettings is null) throw new AnticipatedException("Could not find the Servers settings.");

        guildModerationSettings.MuteRole = command.Role;

        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        return new BaseResponse { LogChannelId = guildModerationSettings.Guild.ModChannelLog };
    }
}
