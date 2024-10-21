// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Leveling.Awards;

public enum XpOption
{
    [ChoiceName("Take all their xp.")]
    All,
    [ChoiceName("Take a specific amount.")]
    Amount
}

public sealed class ReclaimUserXp
{
    [SlashRequireGuild]
    [SlashRequireUserGuildPermissions(DiscordPermissions.ManageMessages)]
    [SlashRequireModuleEnabled(Module.Leveling)]
    public sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Reclaim", "Takes away xp from user.")]
        public async Task ReclaimAsync(InteractionContext ctx,
            [Option("User", "User to take xp away from.")] DiscordUser user,
            [Option("Option", "Select either to take all of their xp or a specific amount.")]
            XpOption option,
            [Minimum(0)]
        [Option("Amount", "The amount of xp to Take.")] long amount = 0)
        {
            await ctx.DeferAsync();
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

            await ctx.EditReplyAsync(GrimoireColor.DarkPurple, message: $"{response.XpTaken} xp has been taken from {user.Mention}.");
            await ctx.SendLogAsync(response, GrimoireColor.Purple,
                message: $"{response.XpTaken} xp has been taken from {user.Mention} by {ctx.Member.Mention}.");
        }
    }
    public sealed record Request : ICommand<Response>
    {
        public required XpOption XpOption { get; init; }
        public required long XpToTake { get; init; }
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
        public ulong? ReclaimerId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : ICommandHandler<Request, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            var member = await this._grimoireDbContext.Members
            .AsNoTracking()
            .WhereMemberHasId(command.UserId, command.GuildId)
            .Select(x => new
            {
                Xp = x.XpHistory.Sum(x => x.Xp ),
                x.Guild.ModChannelLog
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (member is null)
                throw new AnticipatedException($"{UserExtensions.Mention(command.UserId)} was not found. Have they been on the server before?");

            var xpToTake = command.XpOption switch
            {
                XpOption.All => member.Xp,
                XpOption.Amount => command.XpToTake,
                _ => throw new ArgumentOutOfRangeException(nameof(command),"XpOption not implemented in switch statement.")
            };

            xpToTake = Math.Min(member.Xp, xpToTake);

            await this._grimoireDbContext.XpHistory.AddAsync(new XpHistory
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                Xp = -xpToTake,
                Type = XpHistoryType.Reclaimed,
                AwarderId = command.ReclaimerId,
                TimeOut = DateTimeOffset.UtcNow
            }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            return new Response
            {
                LogChannelId = member.ModChannelLog,
                XpTaken = xpToTake
            };
        }
    }

    public sealed record Response : BaseResponse
    {
        public required long XpTaken { get; init; }
    }

}

