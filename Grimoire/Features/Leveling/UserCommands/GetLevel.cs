// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Shared.Queries;

namespace Grimoire.Features.Leveling.UserCommands;

public sealed class GetLevel
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Leveling)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>
        ///
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="user"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [SlashCommand("Level", "Gets the leveling details for the user.")]
        public async Task LevelAsync(
            InteractionContext ctx,
            [Option("user", "User to get details from. Blank will return your info.")] DiscordUser? user = null)
        {
            var userCommandChannel = await _mediator.Send(new GetUserCommandChannel.Query{ GuildId = ctx.Guild.Id });

            await ctx.DeferAsync(!ctx.Member.Permissions.HasPermission(DiscordPermissions.ManageMessages)
               && userCommandChannel?.UserCommandChannelId != ctx.Channel.Id);
            user ??= ctx.User;

            var response = await this._mediator.Send(new Query{ UserId = user.Id, GuildId = ctx.Guild.Id});

            DiscordColor color;
            string displayName;
            string avatarUrl;

            if (user is DiscordMember member)
            {
                color = member.Color;
                displayName = member.DisplayName;
                avatarUrl = member.GetGuildAvatarUrl(ImageFormat.Auto);
            }
            else
            {
                color = user.BannerColor ?? DiscordColor.Blurple;
                displayName = user.Username;
                avatarUrl = user.GetAvatarUrl(ImageFormat.Auto);
            }

            if (string.IsNullOrEmpty(avatarUrl))
                avatarUrl = user.DefaultAvatarUrl;


            DiscordRole? roleReward = null;
            if (response.NextRoleRewardId is not null)
                roleReward = await ctx.Guild.GetRoleAsync(response.NextRoleRewardId.Value);

            var embed = new DiscordEmbedBuilder()
            .WithColor(color)
            .WithTitle($"Level and EXP for {displayName}")
            .AddField("XP", $"{response.UsersXp}", inline: true)
            .AddField("Level", $"{response.UsersLevel}", inline: true)
            .AddField("Progress", $"{response.LevelProgress}/{response.XpForNextLevel}", inline: true)
            .AddField("Next Reward", roleReward is null ? "None" : $"{roleReward.Mention}\n at level {response.NextRewardLevel}", inline: true)
            .WithThumbnail(avatarUrl)
            .WithFooter($"{ctx.Guild.Name}", ctx.Guild.IconUrl)
            .Build();
            await ctx.EditReplyAsync(embed: embed);
        }
    }

    public sealed record Query : IRequest<Response>
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Query, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response> Handle(Query request, CancellationToken cancellationToken)
        {
            var member = await this._grimoireDbContext.Members
            .AsNoTracking()
            .WhereMemberHasId(request.UserId, request.GuildId)
            .Include(x => x.Guild.LevelSettings)
            .Select(Member => new
            {
                Xp = Member.XpHistory.Sum(x => x.Xp),
                Member.Guild.LevelSettings.Base,
                Member.Guild.LevelSettings.Modifier,
                Rewards = Member.Guild.Rewards.OrderBy(reward => reward.RewardLevel)
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
                NextRoleRewardId = nextReward?.RoleId,
            };
        }
    }

    public sealed record Response : BaseResponse
    {
        public long UsersXp { get; init; }
        public int UsersLevel { get; init; }
        public long LevelProgress { get; init; }
        public long XpForNextLevel { get; init; }
        public ulong? NextRoleRewardId { get; init; }
        public int? NextRewardLevel { get; init; }
    }


}

