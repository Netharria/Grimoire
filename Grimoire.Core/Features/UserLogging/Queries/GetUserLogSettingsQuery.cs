// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.UserLogging.Queries;

public sealed record GetUserLogSettingsQuery : IRequest<GetUserLogSettingsQueryResponse>
{
    public ulong GuildId { get; init; }
}


public sealed class GetUserLogSettingsQueryHandler(GrimoireDbContext grimoireDbContext) : IRequestHandler<GetUserLogSettingsQuery, GetUserLogSettingsQueryResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<GetUserLogSettingsQueryResponse> Handle(GetUserLogSettingsQuery request, CancellationToken cancellationToken)
    {
        return await this._grimoireDbContext.GuildUserLogSettings
            .Where(x => x.GuildId == request.GuildId)
            .Select(x => new GetUserLogSettingsQueryResponse
            {
                JoinChannelLog = x.JoinChannelLogId,
                LeaveChannelLog = x.LeaveChannelLogId,
                UsernameChannelLog = x.UsernameChannelLogId,
                NicknameChannelLog = x.NicknameChannelLogId,
                AvatarChannelLog = x.AvatarChannelLogId,
                IsLoggingEnabled = x.ModuleEnabled
            }).FirstAsync(cancellationToken: cancellationToken);
    }
}

public sealed record GetUserLogSettingsQueryResponse : BaseResponse
{
    public ulong? JoinChannelLog { get; init; }
    public ulong? LeaveChannelLog { get; init; }
    public ulong? UsernameChannelLog { get; init; }
    public ulong? NicknameChannelLog { get; init; }
    public ulong? AvatarChannelLog { get; init; }
    public bool IsLoggingEnabled { get; init; }
}
