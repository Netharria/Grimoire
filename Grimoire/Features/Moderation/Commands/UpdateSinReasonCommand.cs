// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Commands;

public sealed record UpdateSinReasonCommand : ICommand<UpdateSinReasonCommandResponse>
{
    public long SinId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public ulong GuildId { get; init; }
}

public sealed class UpdateSinReasonCommandHandler(GrimoireDbContext grimoireDbContext) : ICommandHandler<UpdateSinReasonCommand, UpdateSinReasonCommandResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<UpdateSinReasonCommandResponse> Handle(UpdateSinReasonCommand command, CancellationToken cancellationToken)
    {
        var result = await this._grimoireDbContext.Sins
            .Where(x => x.Id == command.SinId)
            .Where(x => x.GuildId == command.GuildId)
            .Select(x => new
            {
                Sin = x,
                UserName = x.Member.User.UsernameHistories
                .OrderByDescending(x => x.Timestamp)
                .Select(x => x.Username)
                .FirstOrDefault(),
                x.Guild.ModChannelLog
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null) throw new AnticipatedException("Could not find a sin with that ID.");

        result.Sin.Reason = command.Reason;

        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        return new UpdateSinReasonCommandResponse
        {
            SinId = command.SinId,
            SinnerName = result.UserName ?? UserExtensions.Mention(result.Sin.UserId),
            LogChannelId = result.ModChannelLog
        };
    }
}

public sealed record UpdateSinReasonCommandResponse : BaseResponse
{
    public long SinId { get; init; }
    public string SinnerName { get; init; } = string.Empty;
}

