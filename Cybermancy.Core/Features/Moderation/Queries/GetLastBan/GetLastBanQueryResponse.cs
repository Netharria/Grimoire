// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Moderation.Queries.GetBan
{
    public sealed record GetLastBanQueryResponse : BaseResponse
    {
        public ulong UserId { get; set; }
        public long SinId { get; set; }
        public ulong? ModeratorId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public ulong GuildId { get; set; }
        public DateTimeOffset SinOn { get; set; }
        public bool ModerationModuleEnabled { get; set; }
    }
}
