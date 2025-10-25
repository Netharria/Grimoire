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
    public async Task ViewMutesAsync(SlashCommandContext ctx)
    {
        await ctx.DeferResponseAsync(true);

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var muteRole = await this._settingsModule.GetMuteRole(ctx.Guild.Id);
        DiscordRole? role = null;
        if (muteRole is not null) role = ctx.Guild.Roles.GetValueOrDefault(muteRole.Value);
        var users = await this._settingsModule.GetAllMutes(ctx.Guild.Id)
            .Select(mute => ctx.Guild.Members.GetValueOrDefault(mute.UserId))
            .OfType<DiscordMember>()
            .ToArrayAsync();
        var embed = new DiscordEmbedBuilder();

        embed.AddField("Mute Role", role?.Mention ?? "None");
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (users is not null && users.Length != 0)
            embed.AddField("Muted Users", string.Join(" ", users.Select(x => x.Mention)));
        else
            embed.AddField("Muted Users", "None");


        await ctx.EditReplyAsync(embed: embed);
    }
}
