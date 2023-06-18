// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries.GetBan;

public sealed record GetBanQueryResponse : BaseResponse
{
    public ulong BanLogId { get; init; }
    public DateTimeOffset Date { get; init; }
    public string Username { get; init; } = string.Empty;
    public ulong UserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public ulong? PublishedMessage { get; init; }
}
