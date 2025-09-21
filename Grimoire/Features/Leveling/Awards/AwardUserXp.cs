// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Settings;

namespace Grimoire.Features.Leveling.Awards;

public sealed class AwardUserXp
{
    [RequireGuild]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    [RequireModuleEnabled(Module.Leveling)]
    internal sealed class Command(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
        private readonly GuildLog _guildLog = guildLog;

        [Command("Award")]
        [Description("Awards a user some xp.")]
        public async Task AwardAsync(CommandContext ctx,
            [Parameter("User")]
            [Description("The user to award xp.")]
            DiscordMember user,
            [MinMaxValue(0)]
            [Parameter("XP")]
            [Description("The amount of xp to grant.")]
            int xpToAward)
        {
            await ctx.DeferResponseAsync();
            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
            var memberExists = await dbContext.Members
                .AsNoTracking()
                .Where(member => member.UserId == user.Id && member.GuildId == ctx.Guild.Id)
                .AnyAsync();

            if (memberExists is false)
                    throw new AnticipatedException(
                        $"{user.Mention} was not found. Have they been on the server before?");

            await dbContext.XpHistory.AddAsync(
                new XpHistory
                {
                    GuildId = ctx.Guild.Id,
                    UserId = user.Id,
                    Xp = xpToAward,
                    TimeOut = DateTimeOffset.UtcNow,
                    Type = XpHistoryType.Awarded,
                    AwarderId = ctx.User.Id
                });
            await dbContext.SaveChangesAsync();

            await ctx.EditReplyAsync(GrimoireColor.DarkPurple, $"{user.Mention} has been awarded {xpToAward} xp.");
            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Description = $"{user.Mention} has been awarded {xpToAward} xp by {ctx.User.Mention}.",
                Color = GrimoireColor.Purple
            });
        }
    }
}
