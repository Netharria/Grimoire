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
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("Unmute")]
    [Description("Unmutes a user.")]
    public async Task UnmuteUserAsync(
        CommandContext ctx,
        [Parameter("User")] [Description("The user to unmute.")]
        DiscordMember member)
    {
        await ctx.DeferResponseAsync();


        var guild = ctx.Guild!;

        if (guild.GetGuildId() != member.Guild.GetGuildId())
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "The specified user is not in this server.");
            return;
        }

        await this._settingsModule.RemoveMute(member.GetUserId(), guild.GetGuildId());

        var muteRoleId = await this._settingsModule.GetMuteRole(guild.GetGuildId());
        if (muteRoleId is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow,
                "The mute role is not configured. Please configure it before using this command.");
            return;
        }
        var muteRole = await guild.GetRoleOrDefaultAsync(muteRoleId.Value);
        if (muteRole is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow,
                "The configured mute role does not exist. Please configure it again before using this command.");
            return;
        }
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
                GuildId = guild.GetGuildId(),
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.Red,
                Description =
                    $"Was not able to send a direct message with the unmute details to {member.Mention}"
            });
        }

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = guild.GetGuildId(), GuildLogType = GuildLogType.Moderation, Embed = embed
        });
    }
}
