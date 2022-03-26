// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using MediatR;

namespace Cybermancy.Core.Features.Logging.Queries.MessageLogQueries.GetMessages
{
    public class GetMessagesQuery : IRequest<GetMessagesQueryResponse>
    {
        public ulong[] MesssageIds { get; init; } = Array.Empty<ulong>();
    }
}
