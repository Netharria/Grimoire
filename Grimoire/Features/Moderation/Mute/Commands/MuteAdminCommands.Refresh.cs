// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Mute.Commands;

public partial class MuteAdminCommands
{
    [Command("Refresh")]
    [Description("Refreshes the permissions of the currently configured mute role.")]
    public async Task RefreshMuteRoleAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;
        var response = await this._settingsModule.GetConfiguredMuteRole(guild.GetGuildId());

        if (response.GetOrElse(() => null) is not { } roleId)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "No mute role is configured.");
            return;
        }

        if (!guild.Roles.TryGetValue(roleId.Value, out var role))
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "Could not find configured mute role.");
            return;
        }

        await ctx.ReplyAsync(GrimoireColor.DarkPurple, $"Refreshing permissions for {role.Mention} role.");
        var result = await SetMuteRolePermissionsAsync(guild, role)
            .Where(x => !x.WasSuccessful)
            .ToArrayAsync();

        if (result.Length == 0)
            await ctx.ReplyAsync(GrimoireColor.DarkPurple,
                $"Succussfully refreshed permissions for {role.Mention} role.");
        else
            await ctx.ReplyAsync(GrimoireColor.Yellow,
                $"Was not able to set permissions for the following channels. " +
                $"{string.Join(' ', result.Select(x => x.Channel.Mention))}");
    }
}
