// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Leveling.Commands.ManageXpCommands.ReclaimUserXp
{
    public sealed record ReclaimUserXpCommand : ICommand
    {
        public string XpToTake { get; init; } = string.Empty;
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
        public ulong? ReclaimerId { get; init; }
    }
}
