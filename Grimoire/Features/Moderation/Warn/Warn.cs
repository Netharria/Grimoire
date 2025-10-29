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

namespace Grimoire.Features.Moderation.Warn;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequirePermissions([], [DiscordPermission.ManageMessages])]
internal sealed class Warn(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;

    [Command("Warn")]
    [Description("Issue a warning to the user.")]
    public async Task WarnAsync(CommandContext ctx,
        [Parameter("User")] [Description("The user to warn.")]
        DiscordUser user,
        [MinMaxLength(maxLength: 1000)] [Parameter("Reason")] [Description("The reason for the warn.")]
        string reason)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        if (ctx.User == user)
        {
            await ctx.SendErrorResponseAsync("You cannot warn yourself.");
            return;
        }
        await using var dbcontext = await this._dbContextFactory.CreateDbContextAsync();
        var sin = new Sin
        {
            UserId = user.Id,
            GuildId = guild.Id,
            ModeratorId = ctx.User.Id,
            Reason = reason,
            SinType = SinType.Warn
        };
        await dbcontext.Sins
            .AddAsync(sin);
        await dbcontext.SaveChangesAsync();
        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Warn")
            .AddField("User", user.Mention, true)
            .AddField("Sin Id", $"**{sin.Id}**", true)
            .AddField("Moderator", ctx.User.Mention, true)
            .AddField("Reason", reason)
            .WithColor(GrimoireColor.Yellow)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.EditReplyAsync(embed: embed);

        try
        {
            if (user is DiscordMember member)
                await member.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithAuthor($"Warning Id {sin.Id}")
                    .WithDescription($"You have been warned by {ctx.User.Mention} for {reason}")
                    .WithColor(GrimoireColor.Yellow));
        }
        catch (Exception ex) when (ex is BadRequestException or UnauthorizedException)
        {
            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.Red,
                Description =
                    $"Was not able to send a direct message with the warn details to {user.Mention}"
            });
        }

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = guild.Id, GuildLogType = GuildLogType.Moderation, Embed = embed
        });
    }
}
