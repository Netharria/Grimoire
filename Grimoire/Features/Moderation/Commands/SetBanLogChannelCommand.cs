// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Commands;

public sealed record SetBanLogChannelCommand : IRequest<BaseResponse>
{
    public ulong GuildId { get; init; }
    public ulong? ChannelId { get; init; }
}

public sealed class SetBanLogChannelCommandHandler(GrimoireDbContext grimoireDbContext)
    : IRequestHandler<SetBanLogChannelCommand, BaseResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async Task<BaseResponse> Handle(SetBanLogChannelCommand command, CancellationToken cancellationToken)
    {
        var guildModerationSettings = await this._grimoireDbContext.GuildModerationSettings
            .Include(x => x.Guild)
            .FirstOrDefaultAsync(x => x.GuildId.Equals(command.GuildId), cancellationToken);
        if (guildModerationSettings is null) throw new AnticipatedException("Could not find the Servers settings.");

        guildModerationSettings.PublicBanLog = command.ChannelId;

        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        return new BaseResponse { LogChannelId = guildModerationSettings.Guild.ModChannelLog };
    }
}
