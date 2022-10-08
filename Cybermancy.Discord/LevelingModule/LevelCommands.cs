// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Enums;
using Cybermancy.Core.Features.Leveling.Queries.GetLevel;
using Cybermancy.Discord.Attributes;
using Cybermancy.Discord.Extensions;
using Cybermancy.Discord.Structs;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using MediatR;

namespace Cybermancy.Discord.LevelingModule
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Leveling)]
    public class LevelCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public LevelCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

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
            user ??= ctx.User;
            if (user is not DiscordMember member) return;

            var response = await this._mediator.Send(new GetLevelQuery{ UserId = user.Id, GuildId = member.Guild.Id});

            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }

            DiscordRole? roleReward = null;
            if (response.NextRoleRewardId is not null)
                roleReward = ctx.Guild.GetRole(response.NextRoleRewardId.Value);

            var embed = new DiscordEmbedBuilder()
                .WithColor(member.Color)
                .WithTitle($"Level and EXP for {member.DisplayName}")
                .AddField("XP", $"{response.UsersXp}", inline: true)
                .AddField("Level", $"{response.UsersLevel}", inline: true)
                .AddField("Progress", $"{response.LevelProgress}/{response.XpForNextLevel}", inline: true)
                .AddField("Next Reward", roleReward is null ? "None" : $"{roleReward.Mention}\n at level {response.NextRewardLevel}", inline: true)
                .WithThumbnail(member.AvatarUrl ?? member.DefaultAvatarUrl)
                .WithFooter($"{ctx.Guild.Name}", ctx.Guild.IconUrl)
                .Build();
            await ctx.ReplyAsync(
                embed: embed,
                ephemeral: !member.Permissions.HasPermission(Permissions.ManageMessages));
        }
    }
}
