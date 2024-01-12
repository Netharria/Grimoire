// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Shared.Commands;

public sealed record SetModLogCommand : ICommand<BaseResponse>
{
    public ulong GuildId { get; init; }
    public ulong? ChannelId { get; init; }
}

public sealed class SetModLogCommandHandler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<SetModLogCommand, BaseResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<BaseResponse> Handle(SetModLogCommand command, CancellationToken cancellationToken)
    {
        var guild = await this._grimoireDbContext.Guilds
            .WhereIdIs(command.GuildId)
            .FirstOrDefaultAsync(cancellationToken);
        if (guild is null)
            throw new AnticipatedException("Could not find the settings for this server.");
        guild.ModChannelLog = command.ChannelId;
        this._grimoireDbContext.Guilds.Update(guild);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return new BaseResponse
        {
            LogChannelId = command.ChannelId,
        };
    }
}
