// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Leveling.Commands.ManageXpCommands.GainUserXp;

public sealed record GainUserXpCommandResponse : BaseResponse
{
    public ulong[] EarnedRewards { get; init; } = Array.Empty<ulong>();
    public int PreviousLevel { get; init; }
    public int CurrentLevel { get; init; }
    public ulong? LevelLogChannel { get; init; }
    public bool Success { get; init; } = false;
}
