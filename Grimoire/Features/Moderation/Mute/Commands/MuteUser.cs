// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Mute.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
[RequirePermissions([DiscordPermission.ManageRoles], [])]
public sealed class MuteUser(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildLog guildLog)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("Mute")]
    [Description("Mutes a user for a specified amount of time.")]
    public async Task MuteUserAsync(
        CommandContext ctx,
        [Parameter("User")] [Description("The user to mute.")]
        DiscordMember member,
        [Parameter("DurationType")] [Description("Select whether the duration will be in minutes hours or days")]
        DurationType durationType,
        [MinMaxValue(0)] [Parameter("DurationAmount")] [Description("The amount of time the mute will last.")]
        int durationAmount,
        [MinMaxLength(maxLength: 1000)] [Parameter("Reason")] [Description("The reason for the mute.")]
        string? reason = null
    )
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        if (guild.Id != member.Guild.Id)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "The specified user is not in this server.");
            return;
        }


        var muteRoleId = await this._settingsModule.GetMuteRole(guild.Id);

        if (muteRoleId is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow,
                "The mute role is not configured. Please configure it before using this command.");
            return;
        }

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var muteEndTime = durationType.GetDateTimeOffset(durationAmount);
        var sin = new Sin
        {
            UserId = member.Id,
            GuildId = guild.Id,
            ModeratorId = ctx.User.Id,
            Reason = reason ?? string.Empty,
            SinType = SinType.Mute
        };

        await dbContext.Sins.AddAsync(sin);
        await dbContext.SaveChangesAsync();

        await this._settingsModule.AddMute(member.Id, guild.Id, sin.Id, muteEndTime);

        var muteRole = await guild.GetRoleOrDefaultAsync(muteRoleId.Value);
        if (muteRole is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow,
                "The configured mute role does not exist. Please configure it again before using this command.");
            return;
        }
        await member.GrantRoleAsync(muteRole, reason!);


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

        await ctx.EditReplyAsync(embed: embed);

        try
        {
            await member.SendMessageAsync(new DiscordEmbedBuilder()
                .WithAuthor($"Mute Id {sin.Id}")
                .WithDescription(
                    $"You have been muted for {durationAmount} {durationType} by {ctx.User.Mention} for {reason}")
                .WithColor(GrimoireColor.Red));
        }
        catch (Exception)
        {
            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Description =
                    $"Was not able to send a direct message with the mute details to {member.Mention}.",
                Color = GrimoireColor.Red
            });
        }

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = guild.Id, GuildLogType = GuildLogType.Moderation, Embed = embed
        });
    }
}
