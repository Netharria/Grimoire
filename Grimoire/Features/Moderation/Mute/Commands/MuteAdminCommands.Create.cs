// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.Moderation.Mute.Commands;

public partial class MuteAdminCommands
{
    [Command("Create")]
    [Description("Creates a new role to be use for muting users and set permissions in all channels.")]
    public async Task CreateMuteRoleAsync(SlashCommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var role = await ctx.Guild.CreateRoleAsync("Muted");

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple,
            $"Role {role.Mention} is created. Now Saving role to {ctx.Client.CurrentUser.Mention} configuration.");

        await this._mediator.Send(new SetMuteRole.Request { Role = role.Id, GuildId = ctx.Guild.Id });

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple,
            $"Role {role.Mention} is saved in {ctx.Client.CurrentUser.Mention} configuration. Now setting role permissions");
        var result = await SetMuteRolePermissionsAsync(ctx.Guild, role)
            .Where(x => !x.WasSuccessful)
            .ToArrayAsync();

        if (result.Length == 0)
            await ctx.EditReplyAsync(GrimoireColor.DarkPurple,
                $"Successfully created role {role.Mention} and set permissions for channels");
        else
            await ctx.EditReplyAsync(GrimoireColor.Yellow, $"Successfully created role {role.Mention} but, " +
                                                           $"was not able to set permissions for the following channels. {string.Join(' ', result.Select(x => x.Channel.Mention))}");

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} created {role.Mention} to use as a mute role."
        });
    }
}
