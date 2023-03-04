// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Shared.SharedDtos;
using Cybermancy.Core.Responses;

namespace Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.BulkDeleteMessages
{
    public sealed record BulkDeleteMessageCommandResponse : BaseResponse
    {
        public IEnumerable<MessageDto> Messages { get; init; } = Enumerable.Empty<MessageDto>();
        public ulong? BulkDeleteLogChannelId { get; init; }
        public bool Success { get; init; } = false;
    }
}
