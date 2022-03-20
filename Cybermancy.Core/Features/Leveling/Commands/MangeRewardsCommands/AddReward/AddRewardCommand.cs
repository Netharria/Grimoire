// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Responses;
using MediatR;

namespace Cybermancy.Core.Features.Leveling.Commands.MangeRewardsCommands.AddReward
{
    public class AddRewardCommand : IRequest<BaseResponse>
    {
        public ulong RoleId { get; init; }
        public ulong GuildId { get; init; }
        public uint RewardLevel { get; init; }
    }
}
