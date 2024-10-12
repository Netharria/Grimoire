// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.UserLogging.Commands;

public sealed record UpdateAvatarCommand : ICommand<UpdateAvatarCommandResponse?>
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
    public string AvatarUrl { get; init; } = string.Empty;
}

public sealed class UpdateAvatarCommandHandler(GrimoireDbContext grimoireDbContext) : ICommandHandler<UpdateAvatarCommand, UpdateAvatarCommandResponse?>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<UpdateAvatarCommandResponse?> Handle(UpdateAvatarCommand command, CancellationToken cancellationToken)
    {
        var currentAvatar = await this._grimoireDbContext.Avatars
            .AsNoTracking()
            .Where(x => x.UserId == command.UserId && x.GuildId == command.GuildId)
            .Where(x => x.Member.Guild.UserLogSettings.ModuleEnabled)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => new
            {
                x.FileName,
                x.Member.Guild.UserLogSettings.AvatarChannelLogId
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        if (currentAvatar is null
            || string.Equals(currentAvatar.FileName, command.AvatarUrl, StringComparison.Ordinal))
            return null;

        await this._grimoireDbContext.Avatars.AddAsync(
            new Avatar
            {
                GuildId = command.GuildId,
                UserId = command.UserId,
                FileName = command.AvatarUrl
            }, cancellationToken);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return new UpdateAvatarCommandResponse
        {
            BeforeAvatar = currentAvatar.FileName,
            AfterAvatar = command.AvatarUrl,
            AvatarChannelLogId = currentAvatar.AvatarChannelLogId
        };
    }
}

public sealed record UpdateAvatarCommandResponse : BaseResponse
{
    public string BeforeAvatar { get; init; } = string.Empty;
    public string AfterAvatar { get; init; } = string.Empty;
    public ulong? AvatarChannelLogId { get; init; }
}
