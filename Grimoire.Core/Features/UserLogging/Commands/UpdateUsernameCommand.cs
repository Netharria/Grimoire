// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.UserLogging.Commands;

public sealed record UpdateUsernameCommand : ICommand<UpdateUsernameCommandResponse?>
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
    public string Username { get; init; } = string.Empty;
}

public sealed class UpdateUsernameCommandHandler(GrimoireDbContext grimoireDbContext) : ICommandHandler<UpdateUsernameCommand, UpdateUsernameCommandResponse?>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<UpdateUsernameCommandResponse?> Handle(UpdateUsernameCommand command, CancellationToken cancellationToken)
    {
        var currentUsername = await this._grimoireDbContext.Members
            .AsNoTracking()
            .WhereMemberHasId(command.UserId, command.GuildId)
            .Where(x => x.Guild.UserLogSettings.ModuleEnabled)
            .Select(x => new
            {
                x.User.UsernameHistories.OrderByDescending(x => x.Timestamp).First(x => x.Timestamp < DateTime.UtcNow.AddSeconds(-2)).Username,
                x.Guild.UserLogSettings.UsernameChannelLogId
            }).FirstOrDefaultAsync(cancellationToken: cancellationToken);
        if (currentUsername is null
            || string.Equals(currentUsername.Username, command.Username, StringComparison.CurrentCultureIgnoreCase))
            return null;

        await this._grimoireDbContext.UsernameHistory.AddAsync(
            new UsernameHistory
            {
                UserId = command.UserId,
                Username = command.Username
            }, cancellationToken);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return new UpdateUsernameCommandResponse
        {
            BeforeUsername = currentUsername.Username,
            AfterUsername = command.Username,
            UsernameChannelLogId = currentUsername.UsernameChannelLogId
        };
    }
}

public sealed record UpdateUsernameCommandResponse : BaseResponse
{
    public string BeforeUsername { get; init; } = string.Empty;
    public string AfterUsername { get; init; } = string.Empty;
    public ulong? UsernameChannelLogId { get; init; }
}
