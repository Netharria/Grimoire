// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Responses;

namespace Cybermancy.Core.Features.Leveling.Commands.ManageXpCommands.UpdateIgnoreStateForXpGain
{
    public class UpdateIgnoreStateForXpGainResponse : BaseResponse
    {
        public string Result { get; init; } = string.Empty;
    }
}
