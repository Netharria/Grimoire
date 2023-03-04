// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain;
using Mediator;

namespace Cybermancy.Core.Features.Moderation.Commands.SetAutoPardon
{
    public sealed record SetAutoPardonCommand : ICommand
    {
        public ulong GuildId { get; init; }
        public Duration DurationType { get; init; }
        public long DurationAmount { get; init; }
    }
}
