// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.Kick;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.KickMembers)]
[RequirePermissions([DiscordPermission.KickMembers], [])]
internal sealed class KickUser(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;

    [Command("Kick")]
    [Description("Kick a member from the server.")]
    public async Task KickAsync(CommandContext ctx,
        [Parameter("Member")] [Description("The member to kick.")]
        DiscordMember member,
        [MinMaxLength(maxLength: 1000)] [Parameter("Reason")] [Description("The reason for the kick.")]
        string? reason = null)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        if (ctx.User.Id == member.Id)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "You can't kick yourself.");
            return;
        }

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var sin = new Sin
        {
            UserId = member.GetUserId(),
            GuildId = guild.GetGuildId(),
            ModeratorId = ctx.GetModeratorId(),
            SinType = SinType.Kick,
            ReasonHistory = string.IsNullOrWhiteSpace(reason)
                ? []
                :
                [
                    new SinReasonHistory
                    {
                        SinId = default,
                        Reason = ModerationReason.FromDatabase(reason!),
                        ModeratorId = ctx.GetModeratorId(),
                        SetAt = DateTimeOffset.UtcNow
                    }
                ]
        };
        dbContext.Sins.Add(sin);
        await dbContext.SaveChangesAsync();

        try
        {
            await member.RemoveAsync(reason ?? string.Empty);
        }
        catch (UnauthorizedException)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "I do not have permissions to kick that member.");
            return;
        }

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
