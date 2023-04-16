// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.MessageLoggingCommands.BulkDeleteMessages
{
    public sealed record BulkDeleteMessageCommand : ICommand<BulkDeleteMessageCommandResponse>
    {
        public ulong[] Ids { get; init; } = Array.Empty<ulong>();
        public ulong GuildId { get; init; }
    }
}
