// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.UpdateAvatar;

public class UpdateAvatarCommandHandler : ICommandHandler<UpdateAvatarCommand, UpdateAvatarCommandResponse?>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public UpdateAvatarCommandHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

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
            || currentAvatar.FileName.Equals(command.AvatarUrl, StringComparison.Ordinal))
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
