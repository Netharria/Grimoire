// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Commands.SinAdminCommands.PardonSin;

public sealed record PardonSinCommand : ICommand<PardonSinCommandResponse>
{
    public long SinId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public ulong ModeratorId { get; init; }
    public ulong GuildId { get; init; }
}
