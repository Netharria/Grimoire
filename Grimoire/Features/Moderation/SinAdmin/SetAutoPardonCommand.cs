// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.SinAdmin;

public sealed record SetAutoPardonCommand : IRequest<BaseResponse>
{
    public ulong GuildId { get; init; }
    public TimeSpan DurationAmount { get; init; }
}

public sealed class SetAutoPardonCommandHandler(GrimoireDbContext grimoireDbContext)
    : IRequestHandler<SetAutoPardonCommand, BaseResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async Task<BaseResponse> Handle(SetAutoPardonCommand command, CancellationToken cancellationToken)
    {
        var guildModerationSettings = await this._grimoireDbContext.GuildModerationSettings
            .Include(x => x.Guild)
            .FirstOrDefaultAsync(guildModerationSettings => guildModerationSettings.GuildId.Equals(command.GuildId),
                cancellationToken);
        if (guildModerationSettings is null)
            throw new AnticipatedException("Could not find the Servers settings.");

        guildModerationSettings.AutoPardonAfter = command.DurationAmount;

        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        return new BaseResponse { LogChannelId = guildModerationSettings.Guild.ModChannelLog };
    }
}
