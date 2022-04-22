// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Enums;
using MediatR;

namespace Cybermancy.Core.Features.Shared.Commands.ModuleCommands.EnableModuleCommand
{
    public class EnableModuleCommand : IRequest<EnableModuleCommandResponse>
    {
        public ulong GuildId { get; init; }
        public Module Module { get; init; }
        public bool Enable { get; init; }
    }
}
