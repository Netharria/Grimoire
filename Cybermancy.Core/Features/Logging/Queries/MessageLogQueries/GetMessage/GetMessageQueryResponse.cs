// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Responses;

namespace Cybermancy.Core.Features.Logging.Queries.MessageLogQueries.GetMessage
{
    public class GetMessageQueryResponse : BaseResponse
    {
        public ulong AuthorId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string AuthorAvatarUrl { get; set; } = string.Empty;
        public ulong ChannelId { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public ulong MessageId { get; set; }
        public string MessageContent { get; set; } = string.Empty;
        public ICollection<string> AttachmentUrls { get; set; } = new List<string>();
    }
}
