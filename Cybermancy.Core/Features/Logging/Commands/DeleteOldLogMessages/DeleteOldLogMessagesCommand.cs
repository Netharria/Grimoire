// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using MediatR;

namespace Cybermancy.Core.Features.Logging.Commands.DeleteOldLogMessages
{
    public class DeleteOldLogMessagesCommand : IRequest
    {
        public ulong[] DeletedOldLogMessageIds { get; init; } = Array.Empty<ulong>();
    }
}
