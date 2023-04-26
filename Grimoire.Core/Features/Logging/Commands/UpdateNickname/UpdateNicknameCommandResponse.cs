// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.UpdateNickname
{
    public sealed record UpdateNicknameCommandResponse : BaseResponse
    {
        public string? BeforeNickname { get; init; }
        public string? AfterNickname { get; init; }
        public ulong NicknameChannelLogId { get; init; }
    }
}
