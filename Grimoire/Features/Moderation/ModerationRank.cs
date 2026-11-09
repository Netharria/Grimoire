// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation;

internal static class ModerationRank
{
    /// <summary>
    ///     Why the invoker can't act on <paramref name="target" />, if anything. Discord refuses the bot for anyone at or
    ///     above its top role. The bot refuses a moderator the same way, except for the owner or someone acting on
    ///     themselves. The mute role doesn't count, because it often sits high up and shouldn't protect the muted.
    /// </summary>
    public static async Task<string?> GetRefusalReasonAsync(SettingsModule settingsModule, CommandContext ctx,
        DiscordMember target, string action)
    {
        var guild = ctx.RequiredGuild;
        if (target.Id == guild.OwnerId || guild.CurrentMember.Hierarchy <= target.Hierarchy)
            return $"I do not have permissions to {action} that user.";

        var invoker = ctx.RequiredMember;
        if (invoker.Id == guild.OwnerId || invoker.Id == target.Id)
            return null;

        var muteRoleId = await settingsModule.GetEffectiveMuteRole(guild.GetGuildId()).GetOrElse(() => null);
        var targetRank = target.Roles.Where(role => role.Id != muteRoleId?.Value)
            .Select(role => role.Position)
            .DefaultIfEmpty(0)
            .Max();

        return invoker.Hierarchy <= targetRank ? $"You can't {action} someone ranked at or above you." : null;
    }
}
