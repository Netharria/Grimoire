// -----------------------------------------------------------------------
// <copyright file="LevelSettingsExtensions.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Core.Extensions
{
    using System;
    using Cybermancy.Domain;

    public static class LevelSettingsExtensions
    {
        public static DateTime GetTextTimeout(this GuildLevelSettings levelSettings)
        {
            return DateTime.UtcNow + TimeSpan.FromMinutes(levelSettings.TextTime);
        }
    }
}