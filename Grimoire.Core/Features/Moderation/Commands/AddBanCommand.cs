// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Moderation.Commands;

public sealed record AddBanCommand : ICommand<AddBanCommandResponse>
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
    public string Reason { get; set; } = string.Empty;
    public ulong? ModeratorId { get; set; }
}

public class AddBanCommandHandler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<AddBanCommand, AddBanCommandResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<AddBanCommandResponse> Handle(AddBanCommand command, CancellationToken cancellationToken)
    {
        var sin = await this._grimoireDbContext.Sins.AddAsync(new Sin
        {
            GuildId = command.GuildId,
            UserId = command.UserId,
            Reason = command.Reason,
            SinType = SinType.Ban,
            ModeratorId = command.ModeratorId
        }, cancellationToken);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        var loggingChannel = await this._grimoireDbContext.Guilds
            .WhereIdIs(command.GuildId)
            .Select(x => x.ModChannelLog)
            .FirstOrDefaultAsync(cancellationToken);
        return new AddBanCommandResponse { SinId = sin.Entity.Id, LogChannelId = loggingChannel };
    }
}

public sealed record AddBanCommandResponse : BaseResponse
{
    public long SinId { get; init; }
}
