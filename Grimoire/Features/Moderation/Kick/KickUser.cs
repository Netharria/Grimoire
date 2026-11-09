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

namespace Grimoire.Features.Moderation.Kick;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.KickMembers)]
[RequirePermissions([DiscordPermission.KickMembers], [])]
internal sealed class KickUser(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    GuildLog guildLog,
    SettingsModule settingsModule)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("Kick")]
    [Description("Kick a member from the server.")]
    public async Task KickAsync(CommandContext ctx,
        [Parameter("member")] [Description("The member to kick.")]
        DiscordMember member,
        [MinMaxLength(maxLength: 1000)] [Parameter("reason")] [Description("The reason for the kick.")]
        string? reason = null)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        if (ctx.User.Id == member.Id)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "You can't kick yourself.");
            return;
        }

        if (await ModerationRank.GetRefusalReasonAsync(this._settingsModule, ctx, member, "kick") is { } refusal)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, refusal);
            return;
        }

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var actor = new ModerationActor.Moderator(ctx.GetModeratorId());
        var now = DateTimeOffset.UtcNow;
        var sin = Sin.ForKick(actor, member.GetUserId(), guild.GetGuildId(), now, ModerationReason.CreateIfNotBlank(reason).Match(r => r, _ => null))
            .Match(s => s, _ => throw new InvariantViolationException("Discord ids are never zero."));

        // Once they're gone the bot usually can't reach them, so the DM goes first.
        await this._guildLog.DirectMessageAsync(member, new DiscordEmbedBuilder()
            .WithAuthor("Kicked")
            .WithDescription($"You have been kicked from {guild.Name}{ModerationDirectMessage.ForReason(reason)}")
            .WithColor(GrimoireColor.Yellow), "kick");

        // Kick first so a refused kick (e.g. missing permissions) does not leave a kick recorded.
        await member.RemoveAsync(reason ?? string.Empty);

        dbContext.Sins.Add(sin);
        await dbContext.SaveChangesAsync();

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Kick")
            .AddField("User", member.Mention, true)
            .AddField("Sin Id", $"**{sin.Id}**", true)
            .AddField("Moderator", ctx.User.Mention, true)
            .WithColor(GrimoireColor.Yellow)
            .WithTimestamp(DateTimeOffset.UtcNow);

        if (!string.IsNullOrWhiteSpace(reason))
            embed.AddField("Reason", reason);

        await ctx.ReplyAsync(embed: embed);

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = guild.GetGuildId(), GuildLogType = GuildLogType.Moderation, Embed = embed
        });
    }
}
