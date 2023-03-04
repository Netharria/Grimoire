// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Responses;

namespace Cybermancy.Core.Features.Moderation.Commands.SinAdminCommands.PardonSin
{
    public sealed record PardonSinCommandResponse : BaseResponse
    {
        public long SinId { get; init; }
        public string SinnerName { get; init; } = string.Empty;
    }
}
