// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Moderation.Commands;

public sealed record ForgetSinCommand : ICommand<ForgetSinCommandResponse>
{
    public long SinId { get; init; }
    public ulong GuildId { get; init; }
}

public sealed class ForgetSinCommandHandler(GrimoireDbContext grimoireDbContext) : ICommandHandler<ForgetSinCommand, ForgetSinCommandResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<ForgetSinCommandResponse> Handle(ForgetSinCommand command, CancellationToken cancellationToken)
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


        this._grimoireDbContext.Sins.Remove(result.Sin);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        return new ForgetSinCommandResponse
        {
            SinId = command.SinId,
            SinnerName = result.UserName ?? UserExtensions.Mention(result.Sin.UserId),
            LogChannelId = result.ModChannelLog
        };
    }
}

public sealed record ForgetSinCommandResponse : BaseResponse
{
    public long SinId { get; init; }
    public string SinnerName { get; init; } = string.Empty;
}
