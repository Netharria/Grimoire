// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.Leveling.Awards;

public sealed class ReclaimUserXp
{
    public enum XpOption
    {
        [ChoiceDisplayName("Take all their xp.")]
        All,

        [ChoiceDisplayName("Take a specific amount.")]
        Amount
    }

    [RequireGuild]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    [RequireModuleEnabled(Module.Leveling)]
    public sealed class Command(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
        private readonly GuildLog _guildLog = guildLog;

        [Command("Reclaim")]
        [Description("Takes away xp from user.")]
        public async Task ReclaimAsync(CommandContext ctx,
            [Parameter("User")] [Description("The user to take xp from.")]
            DiscordUser user,
            [Parameter("Option")] [Description("Select either to take all of their xp or a specific amount.")]
            XpOption option,
            [MinMaxValue(0)] [Parameter("Amount")] [Description("The amount of xp to take.")]
            int amount = 0)
        {
            await ctx.DeferResponseAsync();
            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");
            if (option == XpOption.Amount && amount == 0)
                throw new AnticipatedException("Specify an amount greater than 0");
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
            var member = await dbContext.Members
                .AsNoTracking()
                .Where(member => member.UserId == user.Id && member.GuildId == ctx.Guild.Id)
                .Select(member => new { Xp = member.XpHistory.Sum(xpHistory => xpHistory.Xp) })
                .FirstOrDefaultAsync();
            if (member is null)
                throw new AnticipatedException(
                    $"{user.Id} was not found. Have they been on the server before?");

            var xpToTake = option switch
            {
                XpOption.All => member.Xp,
                XpOption.Amount => amount,
                _ => throw new ArgumentOutOfRangeException(nameof(option),
                    "XpOption not implemented in switch statement.")
            };

            xpToTake = Math.Min(member.Xp, xpToTake);

            await dbContext.XpHistory.AddAsync(
                new XpHistory
                {
                    UserId = user.Id,
                    GuildId = ctx.Guild.Id,
                    Xp = -xpToTake,
                    Type = XpHistoryType.Reclaimed,
                    AwarderId = ctx.User.Id,
                    TimeOut = DateTimeOffset.UtcNow
                });
            await dbContext.SaveChangesAsync();

            await ctx.EditReplyAsync(GrimoireColor.DarkPurple,
                $"{xpToTake} xp has been taken from {user.Mention}.");
            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Description = $"{xpToTake} xp has been taken from {user.Mention} by {ctx.User.Mention}.",
                Color = GrimoireColor.Purple
            });
        }
    }
}
