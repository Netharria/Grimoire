// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Utilities;

public sealed class MuteAdminCommands
{
    internal sealed class OverwriteChannelResult
    {
        public required bool WasSuccessful { get; init; }
        public required DiscordChannel Channel { get; init; }
    }
}

