// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Leveling.Awards;

public sealed class ReclaimUserXp
{
    public enum XpOption
    {
        [ChoiceDisplayName("Take all their xp.")]All,
        [ChoiceDisplayName("Take a specific amount.")]Amount
    }

    [RequireGuild]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    [RequireModuleEnabled(Module.Leveling)]
    public sealed class Command(IMediator mediator)
    {
        private readonly IMediator _mediator = mediator;

        [Command("Reclaim")]
        [Description("Takes away xp from user.")]
        public async Task ReclaimAsync(CommandContext ctx,
            [Parameter("User")]
            [Description("The user to take xp from.")]
            DiscordUser user,
            [Parameter("Option")]
            [Description( "Select either to take all of their xp or a specific amount.")]
            XpOption option,
            [MinMaxValue(0)]
            [Parameter("Amount")]
            [Description("The amount of xp to take.")]
            int amount = 0)
        {
            await ctx.DeferResponseAsync();
            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");
            if (option == XpOption.Amount && amount == 0)
                throw new AnticipatedException("Specify an amount greater than 0");
            var response = await this._mediator.Send(
                new Request
                {
                    UserId = user.Id,
                    GuildId = ctx.Guild.Id,
                    XpToTake = amount,
                    XpOption = option,
                    ReclaimerId = ctx.User.Id
                });

            await ctx.EditReplyAsync(GrimoireColor.DarkPurple,
                $"{response.XpTaken} xp has been taken from {user.Mention}.");
            await ctx.SendLogAsync(response, GrimoireColor.Purple,
                message: $"{response.XpTaken} xp has been taken from {user.Mention} by {ctx.User.Mention}.");
        }
    }

    public sealed record Request : IRequest<Response>
    {
        public required XpOption XpOption { get; init; }
        public required long XpToTake { get; init; }
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
        public ulong? ReclaimerId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var member = await dbContext.Members
                .AsNoTracking()
                .WhereMemberHasId(command.UserId, command.GuildId)
                .Select(member =>
                    new { Xp = member.XpHistory.Sum(xpHistory => xpHistory.Xp), member.Guild.ModChannelLog })
                .FirstOrDefaultAsync(cancellationToken);
            if (member is null)
                throw new AnticipatedException(
                    $"{UserExtensions.Mention(command.UserId)} was not found. Have they been on the server before?");

            var xpToTake = command.XpOption switch
            {
                XpOption.All => member.Xp,
                XpOption.Amount => command.XpToTake,
                _ => throw new ArgumentOutOfRangeException(nameof(command),
                    "XpOption not implemented in switch statement.")
            };

            xpToTake = Math.Min(member.Xp, xpToTake);

            await dbContext.XpHistory.AddAsync(
                new XpHistory
                {
                    UserId = command.UserId,
                    GuildId = command.GuildId,
                    Xp = -xpToTake,
                    Type = XpHistoryType.Reclaimed,
                    AwarderId = command.ReclaimerId,
                    TimeOut = DateTimeOffset.UtcNow
                }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response { LogChannelId = member.ModChannelLog, XpTaken = xpToTake };
        }
    }

    public sealed record Response : BaseResponse
    {
        public required long XpTaken { get; init; }
    }
}
