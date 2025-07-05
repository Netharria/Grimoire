// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Leveling.Awards;

public sealed class AwardUserXp
{
    [RequireGuild]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    [RequireModuleEnabled(Module.Leveling)]
    internal sealed class Command(IMediator mediator)
    {
        private readonly IMediator _mediator = mediator;

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
            if(ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");
            var response = await this._mediator.Send(
                new Request
                {
                    UserId = user.Id, GuildId = ctx.Guild.Id, XpToAward = xpToAward, AwarderId = ctx.User.Id
                });

            await ctx.EditReplyAsync(GrimoireColor.DarkPurple, $"{user.Mention} has been awarded {xpToAward} xp.");
            await ctx.SendLogAsync(response, GrimoireColor.Purple,
                message: $"{user.Mention} has been awarded {xpToAward} xp by {ctx.User.Mention}.");
        }
    }

    public sealed record Request : IRequest<BaseResponse>
    {
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
        public required long XpToAward { get; init; }
        public ulong? AwarderId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Request command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var member = await dbContext.Members
                .AsNoTracking()
                .WhereMemberHasId(command.UserId, command.GuildId)
                .Select(x => new { x.Guild.ModChannelLog })
                .FirstOrDefaultAsync(cancellationToken);

            if (member is null)
                throw new AnticipatedException(
                    $"{UserExtensions.Mention(command.UserId)} was not found. Have they been on the server before?");

            await dbContext.XpHistory.AddAsync(
                new XpHistory
                {
                    GuildId = command.GuildId,
                    UserId = command.UserId,
                    Xp = command.XpToAward,
                    TimeOut = DateTimeOffset.UtcNow,
                    Type = XpHistoryType.Awarded,
                    AwarderId = command.AwarderId
                }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { LogChannelId = member.ModChannelLog };
        }
    }
}
