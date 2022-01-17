// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Responses;

namespace Cybermancy.Core.Features.Leveling.Commands.GainUserXp
{
    public class GainUserXpCommandResponse : BaseResponse
    {
        public ulong[] EarnedRewards { get; init; } = Array.Empty<ulong>();
        public int PreviousLevel { get; init; }
        public int CurrentLevel { get; init; }
        public ulong? LoggingChannel { get; init; }
    }
}
