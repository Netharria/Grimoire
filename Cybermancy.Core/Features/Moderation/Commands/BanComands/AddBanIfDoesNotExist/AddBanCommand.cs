// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Responses;
using Mediator;

namespace Cybermancy.Core.Features.Moderation.Commands.BanComands.AddBanIfDoesNotExist
{
    public sealed record AddBanCommand : ICommand<AddBanCommandResponse>
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
        public string Reason { get; set; } = string.Empty;
        public ulong? ModeratorId { get; set; }
    }
}
