// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using Cybermancy.Domain;

namespace Cybermancy.Core.Extensions
{
    public static class LevelSettingsExtensions
    {
        public static DateTime GetTextTimeout(this GuildLevelSettings levelSettings) => DateTime.UtcNow + TimeSpan.FromMinutes(levelSettings.TextTime);
    }
}