// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Queries.GetMessageLogSettings;

public sealed record GetMessageLogSettingsQueryResponse : BaseResponse
{
    public ulong? EditChannelLog { get; init; }
    public ulong? DeleteChannelLog { get; init; }
    public ulong? BulkDeleteChannelLog { get; init; }
    public bool IsLoggingEnabled { get; init; }
}
