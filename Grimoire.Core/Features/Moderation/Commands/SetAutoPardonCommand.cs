// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Commands;

public sealed record SetAutoPardonCommand : ICommand<BaseResponse>
{
    public ulong GuildId { get; init; }
    public TimeSpan DurationAmount { get; init; }
}


public class SetAutoPardonCommandHandler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<SetAutoPardonCommand, BaseResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<BaseResponse> Handle(SetAutoPardonCommand command, CancellationToken cancellationToken)
    {
        var guildModerationSettings = await this._grimoireDbContext.GuildModerationSettings
            .Include(x => x.Guild)
            .FirstOrDefaultAsync(guildModerationSettings => guildModerationSettings.GuildId.Equals(command.GuildId),
            cancellationToken);
        if (guildModerationSettings is null)
            throw new AnticipatedException("Could not find the Servers settings.");

        guildModerationSettings.AutoPardonAfter = command.DurationAmount;
        this._grimoireDbContext.GuildModerationSettings.Update(guildModerationSettings);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        return new BaseResponse
        {
            LogChannelId = guildModerationSettings.Guild.ModChannelLog
        };
    }
}