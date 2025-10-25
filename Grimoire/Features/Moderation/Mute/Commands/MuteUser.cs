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
    private readonly SettingsModule _settingsModule = settingsModule;
    private readonly GuildLog _guildLog = guildLog;

    [Command("Mute")]
    [Description("Mutes a user for a specified amount of time.")]
    public async Task MuteUserAsync(
        SlashCommandContext ctx,
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

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        if (ctx.Guild.Id != member.Guild.Id) throw new AnticipatedException("That user is not on the server.");


        var muteRoleId = await this._settingsModule.GetMuteRole(ctx.Guild.Id);

        if (muteRoleId is null)
            throw new AnticipatedException("A mute role is not configured for this server.");

        var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var muteEndTime = durationType.GetDateTimeOffset(durationAmount);
        var sin = new Sin
        {
            UserId = member.Id,
            GuildId = ctx.Guild.Id,
            ModeratorId = ctx.User.Id,
            Reason = reason ?? string.Empty,
            SinType = SinType.Mute
        };

        await dbContext.Sins.AddAsync(sin);
        await dbContext.SaveChangesAsync();

        await this._settingsModule.AddMute(member.Id, ctx.Guild.Id, sin.Id, muteEndTime);

        var muteRole = ctx.Guild.Roles.GetValueOrDefault(muteRoleId.Value);
        if (muteRole is null) throw new AnticipatedException("Did not find the configured mute role.");
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
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Description =
                    $"Was not able to send a direct message with the mute details to {member.Mention}.",
                Color = GrimoireColor.Red
            });
        }

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = ctx.Guild.Id, GuildLogType = GuildLogType.Moderation, Embed = embed
        });
    }
}
