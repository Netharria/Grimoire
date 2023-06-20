// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries.GetModerationSettings;
public sealed record GetModerationSettingsQuery : IQuery<GetModerationSettingsQueryResponse>
{
    public ulong GuildId { get; init; }
}
