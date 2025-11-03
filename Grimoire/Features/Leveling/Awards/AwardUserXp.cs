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

namespace Grimoire.Features.Leveling.Awards;

internal sealed class AwardUserXp(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;

    [RequireGuild]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    [RequireModuleEnabled(Module.Leveling)]
    [Command("Award")]
    [Description("Awards a user some xp.")]
    public async Task AwardAsync(CommandContext ctx,
        [Parameter("User")] [Description("The user to award xp.")]
        DiscordMember user,
        [MinMaxValue(0)] [Parameter("XP")] [Description("The amount of xp to grant.")]
        int xpToAward)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        await dbContext.XpHistory.AddAsync(
            new XpHistory
            {
                GuildId = guild.GetGuildId(),
                UserId = user.GetUserId(),
                Xp = xpToAward,
                TimeOut = DateTimeOffset.UtcNow,
                Type = XpHistoryType.Awarded,
                AwarderId = ctx.GetModeratorId()
            });
        await dbContext.SaveChangesAsync();

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, $"{user.Mention} has been awarded {xpToAward} xp.");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Description = $"{user.Mention} has been awarded {xpToAward} xp by {ctx.User.Mention}.",
            Color = GrimoireColor.Purple
        });
    }
}
