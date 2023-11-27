// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.UserLogging.Commands;

public sealed record UpdateNicknameCommand : ICommand<UpdateNicknameCommandResponse?>
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
    public string? Nickname { get; init; }
}

public class UpdateNicknameCommandHandler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<UpdateNicknameCommand, UpdateNicknameCommandResponse?>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<UpdateNicknameCommandResponse?> Handle(UpdateNicknameCommand command, CancellationToken cancellationToken)
    {
        var currentNickname = await this._grimoireDbContext.NicknameHistory
            .AsNoTracking()
            .WhereMemberHasId(command.UserId, command.GuildId)
            .Where(x => x.Guild.UserLogSettings.ModuleEnabled)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => new
            {
                x.Nickname,
                x.Guild.UserLogSettings.NicknameChannelLogId
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        if (currentNickname is null
            || string.Equals(currentNickname.Nickname, command.Nickname, StringComparison.CurrentCultureIgnoreCase))
            return null;

        await this._grimoireDbContext.NicknameHistory.AddAsync(
            new NicknameHistory
            {
                GuildId = command.GuildId,
                UserId = command.UserId,
                Nickname = command.Nickname
            }, cancellationToken);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return new UpdateNicknameCommandResponse
        {
            BeforeNickname = currentNickname.Nickname,
            AfterNickname = command.Nickname,
            NicknameChannelLogId = currentNickname.NicknameChannelLogId
        };
    }
}

public sealed record UpdateNicknameCommandResponse : BaseResponse
{
    public string? BeforeNickname { get; init; }
    public string? AfterNickname { get; init; }
    public ulong? NicknameChannelLogId { get; init; }
}
