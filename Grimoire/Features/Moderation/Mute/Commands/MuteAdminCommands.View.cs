// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Mute.Commands;

public partial class MuteAdminCommands
{
    [Command("View")]
    [Description("View the current configured mute role and any active mutes.")]
    public async Task ViewMutesAsync(CommandContext ctx)
    {
        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(true);
        else
            await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        var muteRole = await this._settingsModule.GetConfiguredMuteRole(guild.GetGuildId())
            .GetOrElse(() => null);
        var role = await guild.GetRoleOrDefaultAsync(muteRole);
        var now = DateTimeOffset.UtcNow;
        var users = await Task.WhenAll(await this._settingsModule.GetAllMutes(guild.GetGuildId())
            .Where(mute => mute.EndTime > now)
            .Select(mute => DescribeMutedUserAsync(guild, mute.UserId))
            .ToArrayAsync());
        // Every page shows the mute role; the muted users are listed below it, as many pages as they need.
        await ctx.ReplyPagedAsync(
            new DiscordEmbedBuilder().AddField("Mute Role", role?.Mention ?? "None"),
            ["**Muted Users**", .. users.Length == 0 ? ["None"] : users],
            ephemeral: true);
    }

    private static async Task<string> DescribeMutedUserAsync(DiscordGuild guild, UserId userId)
        => await guild.GetMemberOrDefaultAsync(userId) is { } member
            ? member.Mention
            : $"`{userId}` (left the server)";
}
