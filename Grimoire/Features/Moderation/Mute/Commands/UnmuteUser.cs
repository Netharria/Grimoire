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
        [Parameter("user")] [Description("The user to unmute.")]
        DiscordMember member)
    {
        await ctx.DeferResponseAsync();


        var guild = ctx.Guild!;

        if (guild.GetGuildId() != member.Guild.GetGuildId())
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "The specified user is not in this server.");
            return;
        }

        var activeMute = await this._settingsModule.GetActiveMute(member.GetUserId(), guild.GetGuildId())
            .GetOrElse(() => null!);
        var configuredRoleId = await this._settingsModule.GetEffectiveMuteRole(guild.GetGuildId())
            .GetOrElse(() => null);
        var muteRole = MuteRoleResolver.Resolve(activeMute?.MuteRoleId, configuredRoleId,
                id => guild.Roles.ContainsKey(id.Value)) is { } muteRoleId
            ? guild.Roles[muteRoleId.Value]
            : null;

        // A mute role with no record, such as one added by hand, can still be taken off.
        if (activeMute is null && (muteRole is null || member.Roles.All(role => role.Id != muteRole.Id)))
        {
            await ctx.SendWarningResponseAsync($"{member.Mention} is not muted.");
            return;
        }

        if (muteRole is null)
        {
            // There is no role to take off, so clear the record: leaving it would keep the member marked as muted.
            await this._settingsModule.RemoveMute(member.GetUserId(), guild.GetGuildId(), ctx.GetModeratorId());
            await ctx.ReplyAsync(GrimoireColor.Yellow,
                "The role this member was muted with no longer exists and no mute role is set, so I could not remove it from "
                + $"{member.Mention}. Their mute is cleared; remove any mute role they still have by hand.");
            return;
        }

        // Take the role off first and only then clear the record. If Discord refuses (for example the bot's role is
        // below the mute role) the error is reported and the mute stays recorded, so it can be retried.
        await member.RevokeRoleAsync(muteRole, $"Unmuted by {ctx.User.Mention}");
        await this._settingsModule.RemoveMute(member.GetUserId(), guild.GetGuildId(), ctx.GetModeratorId());

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Unmute")
            .AddField("User", member.Mention, true)
            .AddField("Moderator", ctx.User.Mention, true)
            .WithColor(GrimoireColor.Green)
            .WithTimestamp(DateTimeOffset.UtcNow);


        await ctx.ReplyAsync(embed: embed);

        await this._guildLog.DirectMessageAsync(member, new DiscordEmbedBuilder()
            .WithAuthor("Unmuted")
            .WithDescription($"You have been unmuted by {ctx.User.Mention}")
            .WithColor(GrimoireColor.Green), "unmute");

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = guild.GetGuildId(), GuildLogType = GuildLogType.Moderation, Embed = embed
        });
    }
}
