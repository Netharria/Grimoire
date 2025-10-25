// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Mute.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
[RequirePermissions([DiscordPermission.ManageRoles], [])]
internal sealed class UnmuteUser(SettingsModule settingsModule, GuildLog guildLog)
{
    private readonly SettingsModule _settingsModule = settingsModule;
    private readonly GuildLog _guildLog = guildLog;

    [Command("Unmute")]
    [Description("Unmutes a user.")]
    public async Task UnmuteUserAsync(
        SlashCommandContext ctx,
        [Parameter("User")] [Description("The user to unmute.")]
        DiscordMember member)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        if (ctx.Guild.Id != member.Guild.Id) throw new AnticipatedException("That user is not on the server.");

        await this._settingsModule.RemoveMute(member.Id, ctx.Guild.Id);

        var muteRoleId = await this._settingsModule.GetMuteRole(ctx.Guild.Id);
        if (muteRoleId is null)
            throw new AnticipatedException("A mute role is not currently configured for this server.");
        var muteRole = ctx.Guild.Roles.GetValueOrDefault(muteRoleId.Value);
        if (muteRole is null) throw new AnticipatedException("Did not find the configured mute role.");
        await member.RevokeRoleAsync(muteRole, $"Unmuted by {ctx.User.Mention}");

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Unmute")
            .AddField("User", member.Mention, true)
            .AddField("Moderator", ctx.User.Mention, true)
            .WithColor(GrimoireColor.Green)
            .WithTimestamp(DateTimeOffset.UtcNow);


        await ctx.EditReplyAsync(embed: embed);

        try
        {
            await member.SendMessageAsync(new DiscordEmbedBuilder()
                .WithAuthor("Unmuted")
                .WithDescription($"You have been unmuted by {ctx.User.Mention}")
                .WithColor(GrimoireColor.Green));
        }
        catch (Exception)
        {
            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.Red,
                Description =
                    $"Was not able to send a direct message with the unmute details to {member.Mention}"
            });
        }

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = ctx.Guild.Id, GuildLogType = GuildLogType.Moderation, Embed = embed
        });
    }
}
