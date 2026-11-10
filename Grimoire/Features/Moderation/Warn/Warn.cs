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

namespace Grimoire.Features.Moderation.Warn;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequirePermissions([], [DiscordPermission.ManageMessages])]
internal sealed class Warn(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
{
    [Command("Warn")]
    [Description("Issue a warning to the user.")]
    public async Task WarnAsync(CommandContext ctx,
        [Parameter("user")] [Description("The user to warn.")]
        DiscordUser user,
        [MinMaxLength(maxLength: 1000)] [Parameter("reason")] [Description("The reason for the warn.")]
        string reason)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        if (ctx.User == user)
        {
            await ctx.SendErrorResponseAsync("You cannot warn yourself.");
            return;
        }

        // M-7: a blank reason used to save the sin and then throw, leaving no reply and no log.
        if (ModerationReason.Create(reason) is not Validation<ModerationReason>.Valid(var validReason))
        {
            await ctx.SendErrorResponseAsync("The reason can't be empty.");
            return;
        }

        await using var dbcontext = await dbContextFactory.CreateDbContextAsync();
        var actor = new ModerationActor.Moderator(ctx.GetModeratorId());
        var now = DateTimeOffset.UtcNow;
        var sin = Sin.ForWarn(actor, user.GetUserId(), guild.GetGuildId(), now, validReason)
            .Match(s => s, _ => throw new InvariantViolationException("Discord ids are never zero."));
        dbcontext.Sins.Add(sin);
        await dbcontext.SaveChangesAsync();
        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Warn")
            .AddField("User", user.Mention, true)
            .AddField("Sin Id", $"**{sin.Id}**", true)
            .AddField("Moderator", ctx.User.Mention, true)
            .AddField("Reason", validReason.Value)
            .WithColor(GrimoireColor.Yellow)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.ReplyAsync(embed: embed);

        if (await guild.GetMemberOrDefaultAsync(user.GetUserId()) is { } member)
            await guildLog.DirectMessageAsync(member, new DiscordEmbedBuilder()
                .WithAuthor($"Warning Id {sin.Id}")
                .WithDescription($"You have been warned by {ctx.User.Mention} for {validReason}")
                .WithColor(GrimoireColor.Yellow), "warn");

        await guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = guild.GetGuildId(), GuildLogType = GuildLogType.Moderation, Embed = embed
        });
    }
}
