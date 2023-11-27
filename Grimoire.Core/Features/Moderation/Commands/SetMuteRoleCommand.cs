// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Commands;

public sealed record SetMuteRoleCommand : ICommand<BaseResponse>
{
    public ulong Role { get; init; }
    public ulong GuildId { get; init; }
}

public class SetMuteRoleCommandHandler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<SetMuteRoleCommand, BaseResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<BaseResponse> Handle(SetMuteRoleCommand command, CancellationToken cancellationToken)
    {
        var guildModerationSettings = await this._grimoireDbContext.GuildModerationSettings
            .Include(x => x.Guild)
            .FirstOrDefaultAsync(x => x.GuildId == command.GuildId, cancellationToken);
        if (guildModerationSettings is null) throw new AnticipatedException("Could not find the Servers settings.");

        guildModerationSettings.MuteRole = command.Role;
        this._grimoireDbContext.GuildModerationSettings.Update(guildModerationSettings);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        return new BaseResponse
        {
            LogChannelId = guildModerationSettings.Guild.ModChannelLog
        };
    }
}
