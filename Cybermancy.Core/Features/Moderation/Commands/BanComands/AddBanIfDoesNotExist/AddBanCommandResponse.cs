// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cybermancy.Core.Responses;

namespace Cybermancy.Core.Features.Moderation.Commands.BanComands.AddBanIfDoesNotExist
{
    public sealed record AddBanCommandResponse : BaseResponse
    {
        public long SinId { get; init; }
    }
}
