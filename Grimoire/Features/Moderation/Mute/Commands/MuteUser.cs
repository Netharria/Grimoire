// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.Mute.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
[RequirePermissions([DiscordPermission.ManageRoles], [])]
public sealed partial class MuteUser(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildLog guildLog,
    ILogger<MuteUser> logger)
{
    private readonly ILogger<MuteUser> _logger = logger;

    [Command("Mute")]
    [Description("Mutes a user for a specified amount of time.")]
    public async Task MuteUserAsync(
        CommandContext ctx,
        [Parameter("user")] [Description("The user to mute.")]
        DiscordMember member,
        [Parameter("duration_type")] [Description("Select whether the duration will be in minutes hours or days")]
        DurationType durationType,
        // 100 years in days. Far more overflows the end time, after nothing has been changed, with a generic error.
        [MinMaxValue(1, 36_500)] [Parameter("duration_amount")] [Description("The amount of time the mute will last.")]
        int durationAmount,
        [MinMaxLength(maxLength: 1000)] [Parameter("reason")] [Description("The reason for the mute.")]
        string? reason = null
    )
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        if (ctx.User.Id == member.Id)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "You can't mute yourself.");
            return;
        }

        if (await ModerationRank.GetRefusalReasonAsync(settingsModule, ctx, member, "mute") is { } refusal)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, refusal);
            return;
        }

        var muteRoleId = await settingsModule.GetEffectiveMuteRole(guild.GetGuildId());

        if (muteRoleId.GetOrElse(() => null) is not { } muteId)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow,
                "The mute role is not configured. Please configure it before using this command.");
            return;
        }

        var muteRole = await guild.GetRoleOrDefaultAsync(muteId);
        if (muteRole is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow,
                "The configured mute role does not exist. Please configure it again before using this command.");
            return;
        }

        var muteEndTime = durationType.GetDateTimeOffset(durationAmount);

        // Grant first so a refused role (e.g. above the bot's) does not leave a mute recorded.
        await member.GrantRoleAsync(muteRole, reason ?? string.Empty);

        var actor = new ModerationActor.Moderator(ctx.GetModeratorId());
        var now = DateTimeOffset.UtcNow;
        var sin = Sin.ForMute(actor, member.GetUserId(), guild.GetGuildId(), now, ModerationReason.CreateIfNotBlank(reason).Match(r => r, _ => null))
            .Match(s => s, _ => throw new InvariantViolationException("Discord ids are never zero."));

        if (!await this.TryRecordMuteAsync(sin, member, ctx.GetModeratorId(), muteEndTime, muteId))
        {
            // Nothing would ever lift a role with no mute on record.
            await member.RevokeRoleAsync(muteRole, "The mute could not be recorded.");
            await ctx.SendErrorResponseAsync("The mute could not be recorded, so it has been undone.");
            return;
        }

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Mute")
            .AddField("User", member.Mention, true)
            .AddField("Sin Id", $"**{sin.Id}**", true)
            .AddField("Moderator", ctx.User.Mention, true)
            .AddField("Length", $"{durationAmount} {durationType}", true)
            .WithColor(GrimoireColor.Red)
            .WithTimestamp(DateTimeOffset.UtcNow);

        if (!string.IsNullOrWhiteSpace(reason))
            embed.AddField("Reason", reason);

        await ctx.ReplyAsync(embed: embed);

        await guildLog.DirectMessageAsync(member, new DiscordEmbedBuilder()
            .WithAuthor($"Mute Id {sin.Id}")
            .WithDescription(
                $"You have been muted for {durationAmount} {durationType} by {ctx.User.Mention}{ModerationDirectMessage.ForReason(reason)}")
            .WithColor(GrimoireColor.Red), "mute");

        await guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = guild.GetGuildId(), GuildLogType = GuildLogType.Moderation, Embed = embed
        });
    }

    /// <summary>
    ///     Saves the sin and then the mute. On false the caller takes the role back off, so any failure, an outage
    ///     included, must land here and not escape: a role with no mute on record is never lifted.
    /// </summary>
    private async Task<bool> TryRecordMuteAsync(Sin sin, DiscordMember member, ModeratorId moderatorId,
        DateTimeOffset muteEndTime, RoleId muteId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            dbContext.Sins.Add(sin);
            await dbContext.SaveChangesAsync();

            if (await settingsModule.AddMute(member.GetUserId(), member.GetGuildId(), moderatorId, sin.Id,
                    muteEndTime, muteId) is Result<MuteAdded>.Success)
                return true;

            // Take the sin back off, so the member's record doesn't show a mute that never happened.
            dbContext.Sins.Remove(sin);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            LogRecordFailure(this._logger, ex, member.GetUserId(), member.GetGuildId());
        }

        return false;
    }

    [LoggerMessage(LogLevel.Error, "Could not record the mute of user {UserId} in guild {GuildId}")]
    static partial void LogRecordFailure(ILogger logger, Exception ex, UserId userId, GuildId guildId);
}
