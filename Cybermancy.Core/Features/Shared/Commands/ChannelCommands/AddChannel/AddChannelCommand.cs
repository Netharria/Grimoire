// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using MediatR;

namespace Cybermancy.Core.Features.Shared.Commands.ChannelCommands.AddChannel
{
    public class AddChannelCommand : IRequest
    {
        public ulong GuildId { get; init; }
        public ulong ChannelId { get; init; }
        public string ChannelName { get; init; } = string.Empty;
    }
}
