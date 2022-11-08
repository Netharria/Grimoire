// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Responses;
using Mediator;

namespace Cybermancy.Core.Features.Leveling.Commands.SetLevelSettings
{
    public sealed record SetLevelSettingsCommand : ICommand<BaseResponse>
    {
        public ulong GuildId { get; init; }
        public LevelSettings LevelSettings { get; init; }
        public string Value { get; init; } = string.Empty;
    }
    public enum LevelSettings
    {
        TextTime,
        Base,
        Modifier,
        Amount,
        LogChannel,
    }
}
