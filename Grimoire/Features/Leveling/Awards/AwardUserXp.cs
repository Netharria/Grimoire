// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Leveling.Awards;

public sealed class AwardUserXp
{
    [SlashRequireGuild]
    [SlashRequireUserGuildPermissions(DiscordPermissions.ManageMessages)]
    [SlashRequireModuleEnabled(Module.Leveling)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Award", "Awards a user some xp.")]
        public async Task AwardAsync(InteractionContext ctx,
            [Option("User", "User to award xp.")] DiscordUser user,
            [Minimum(0)] [Option("XP", "The amount of xp to grant.")]
            long xpToAward)
        {
            await ctx.DeferAsync();
            var response = await this._mediator.Send(
                new Request
                {
                    UserId = user.Id, GuildId = ctx.Guild.Id, XpToAward = xpToAward, AwarderId = ctx.User.Id
                });

            await ctx.EditReplyAsync(GrimoireColor.DarkPurple, $"{user.Mention} has been awarded {xpToAward} xp.");
            await ctx.SendLogAsync(response, GrimoireColor.Purple,
                message: $"{user.Mention} has been awarded {xpToAward} xp by {ctx.Member.Mention}.");
        }
    }

    public sealed record Request : IRequest<BaseResponse>
    {
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
        public required long XpToAward { get; init; }
        public ulong? AwarderId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Request, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<BaseResponse> Handle(Request command, CancellationToken cancellationToken)
        {
            var member = await this._grimoireDbContext.Members
                .AsNoTracking()
                .WhereMemberHasId(command.UserId, command.GuildId)
                .Select(x => new { x.Guild.ModChannelLog })
                .FirstOrDefaultAsync(cancellationToken);

            if (member is null)
                throw new AnticipatedException(
                    $"{UserExtensions.Mention(command.UserId)} was not found. Have they been on the server before?");

            await this._grimoireDbContext.XpHistory.AddAsync(
                new XpHistory
                {
                    GuildId = command.GuildId,
                    UserId = command.UserId,
                    Xp = command.XpToAward,
                    TimeOut = DateTimeOffset.UtcNow,
                    Type = XpHistoryType.Awarded,
                    AwarderId = command.AwarderId
                }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { LogChannelId = member.ModChannelLog };
        }
    }
}
