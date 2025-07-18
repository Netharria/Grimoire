// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Shared.Queries;

namespace Grimoire.Features.Leveling.UserCommands;

public sealed class GetLevel
{
    [RequireGuild]
    [RequireModuleEnabled(Module.Leveling)]
    internal sealed class Command(IMediator mediator)
    {
        private readonly IMediator _mediator = mediator;

        [Command("Level")]
        [Description("Gets the leveling details for the user.")]
        public async Task LevelAsync(
            SlashCommandContext ctx,
            [Parameter("user")]
            [Description("User to get details from. Blank will return your info.")]
            DiscordUser? user = null)
        {
            if(ctx.Guild is null || ctx.Member is null)
                throw new AnticipatedException("This command can only be used in a server.");

            var userCommandChannel =
                await this._mediator.Send(new GetUserCommandChannel.Query { GuildId = ctx.Guild.Id });

            await ctx.DeferResponseAsync(!ctx.Member.Permissions.HasPermission(DiscordPermission.ManageMessages)
                                 && userCommandChannel?.UserCommandChannelId != ctx.Channel.Id);
            user ??= ctx.User;

            var response = await this._mediator.Send(new Query { UserId = user.Id, GuildId = ctx.Guild.Id });

            DiscordColor color;
            string displayName;
            string avatarUrl;

            if (user is DiscordMember member)
            {
                color = member.Color;
                displayName = member.DisplayName;
                avatarUrl = member.GetGuildAvatarUrl(MediaFormat.Auto);
            }
            else
            {
                color = user.BannerColor ?? DiscordColor.Blurple;
                displayName = user.Username;
                avatarUrl = user.GetAvatarUrl(MediaFormat.Auto);
            }

            if (string.IsNullOrEmpty(avatarUrl))
                avatarUrl = user.DefaultAvatarUrl;


            DiscordRole? roleReward = null;
            if (response.NextRoleRewardId is not null)
                roleReward = await ctx.Guild.GetRoleAsync(response.NextRoleRewardId.Value);

            var embed = new DiscordEmbedBuilder()
                .WithColor(color)
                .WithTitle($"Level and EXP for {displayName}")
                .AddField("XP", $"{response.UsersXp}", true)
                .AddField("Level", $"{response.UsersLevel}", true)
                .AddField("Progress", $"{response.LevelProgress}/{response.XpForNextLevel}", true)
                .AddField("Next Reward",
                    roleReward is null ? "None" : $"{roleReward.Mention}\n at level {response.NextRewardLevel}", true)
                .WithThumbnail(avatarUrl)
                .WithFooter($"{ctx.Guild.Name}", ctx.Guild.IconUrl)
                .Build();
            await ctx.EditReplyAsync(embed: embed);
        }
    }

    public sealed record Query : IRequest<Response>
    {
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Query request, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var member = await dbContext.Members
                .AsNoTracking()
                .WhereMemberHasId(request.UserId, request.GuildId)
                .Include(x => x.Guild.LevelSettings)
                .Select(member => new
                {
                    Xp = member.XpHistory.Sum(x => x.Xp),
                    member.Guild.LevelSettings.Base,
                    member.Guild.LevelSettings.Modifier,
                    Rewards = member.Guild.Rewards.OrderBy(reward => reward.RewardLevel)
                        .Select(reward => new { reward.RoleId, reward.RewardLevel })
                }).FirstOrDefaultAsync(cancellationToken);

            if (member is null)
                throw new AnticipatedException("That user could not be found.");

            var currentLevel = MemberExtensions.GetLevel(member.Xp, member.Base, member.Modifier);
            var currentLevelXp = MemberExtensions.GetXpNeeded(currentLevel, member.Base, member.Modifier);
            var nextLevelXp = MemberExtensions.GetXpNeeded(currentLevel, member.Base, member.Modifier, 1);

            var nextReward = member.Rewards.FirstOrDefault(reward => reward.RewardLevel > currentLevel);

            return new Response
            {
                UsersXp = member.Xp,
                UsersLevel = currentLevel,
                LevelProgress = member.Xp - currentLevelXp,
                XpForNextLevel = nextLevelXp - currentLevelXp,
                NextRewardLevel = nextReward?.RewardLevel,
                NextRoleRewardId = nextReward?.RoleId
            };
        }
    }

    public sealed record Response : BaseResponse
    {
        public required long UsersXp { get; init; }
        public required int UsersLevel { get; init; }
        public required long LevelProgress { get; init; }
        public required long XpForNextLevel { get; init; }
        public required ulong? NextRoleRewardId { get; init; }
        public required int? NextRewardLevel { get; init; }
    }
}
