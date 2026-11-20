// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Documents;

namespace Grimoire.Settings.Helpers;

internal static class CacheKey
{
    /// <summary>One entry per guild and document (spec 2.2, C13), so a disabled module's document is never cached.</summary>
    internal static string Document(SettingsDocument document, GuildId guildId) => $"Settings_{document}_{guildId}";
    internal static string ChannelLocks(GuildId guildId) => $"ChannelLocks_{guildId}";
    internal static string ThreadLocks(GuildId guildId) => $"ThreadLocks_{guildId}";
}
