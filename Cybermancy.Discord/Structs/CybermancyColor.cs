// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;

namespace Cybermancy.Discord.Structs
{
    public readonly struct CybermancyColor
    {
        public static readonly DiscordColor Purple = new(108, 0, 209);
        public static readonly DiscordColor Orange = new(252, 119, 3);
        public static readonly DiscordColor Green = new(3, 252, 111);
        public static readonly DiscordColor Gold = new(252, 194, 3);
    }
}
