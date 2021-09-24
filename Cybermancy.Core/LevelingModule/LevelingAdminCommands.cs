// -----------------------------------------------------------------------
// <copyright file="LevelingAdminCommands.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Core.LevelingModule
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cybermancy.Core.Contracts.Services;
    using Cybermancy.Core.Enums;
    using Cybermancy.Core.Extensions;
    using DSharpPlus;
    using DSharpPlus.Entities;
    using DSharpPlus.SlashCommands;
    using DSharpPlus.SlashCommands.Attributes;

    [SlashRequireGuild]
    [SlashRequireUserPermissions(Permissions.ManageMessages)]
    public class LevelingAdminCommands : ApplicationCommandModule
    {
        private readonly IUserLevelService userLevelService;
        private readonly IChannelService channelService;
        private readonly IRoleService roleService;

        /// <summary>
        /// Initializes a new instance of the <see cref="LevelingAdminCommands"/> class.
        /// </summary>
        /// <param name="userLevelService"></param>
        /// <param name="channelService"></param>
        /// <param name="roleService"></param>
        public LevelingAdminCommands(IUserLevelService userLevelService, IChannelService channelService, IRoleService roleService)
        {
            this.userLevelService = userLevelService;
            this.channelService = channelService;
            this.roleService = roleService;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="user"></param>
        /// <param name="amount"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [SlashCommand("Award", "Awards a user some xp.")]
        public async Task AwardAsync(InteractionContext ctx, [Option("User", "User to award xp.")] DiscordUser user, [Option("XP", "The amount of xp to grant.")] int amount)
        {
            if (amount <= 0)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: "XP needs to be a positive value");
                return;
            }

            var userLevel = await this.userLevelService.GetUserLevelAsync(user.Id, ctx.Guild.Id);
            if (userLevel is null)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: $"{user.Mention} was not found. Have they been on the server before?");
                return;
            }

            userLevel.GrantXp(amount);
            await this.userLevelService.SaveAsync(userLevel);
            await ctx.ReplyAsync(CybermancyColor.Gold, message: $"{user.Mention} has been awarded{amount} xp.");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="user"></param>
        /// <param name="amount"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [SlashCommand("Reclaim", "Takes away xp from user.")]
        public async Task ReclaimAsync(InteractionContext ctx, [Option("User", "User to take xp away from.")] DiscordUser user, [Option("XP", "The amount of xp to Take.")] string amount)
        {
            var userLevel = await this.userLevelService.GetUserLevelAsync(user.Id, ctx.Guild.Id);
            if (userLevel is null)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: $"{user.Mention} was not found. Have they been on the server before?");
                return;
            }

            int xpToTake;
            if (amount.Equals("All", StringComparison.CurrentCultureIgnoreCase))
            {
                xpToTake = userLevel.Xp;
            }
            else if (!int.TryParse(amount, out xpToTake))
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: $"XP needs to be a valid number.");
                return;
            }

            if (xpToTake <= 0)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: "XP needs to be a positive value.");
                return;
            }

            userLevel.Xp -= xpToTake;
            await this.userLevelService.SaveAsync(userLevel);
            await ctx.ReplyAsync(CybermancyColor.Gold, message: $"{amount} xp has been taken from {user.Mention}.");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="snowflake"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [SlashCommand("Ignore", "Ignores a user, channel, or role for xp gains")]
        public async Task IgnoreAsync(InteractionContext ctx, [Option("Item", "The user, channel or role to ignore")] SnowflakeObject snowflake)
        {
            if (snowflake is DiscordUser user)
            {
                var userLevel = await this.userLevelService.GetUserLevelAsync(user.Id, ctx.Guild.Id);
                if (userLevel is null)
                {
                    await ctx.ReplyAsync(CybermancyColor.Orange, message: $"{user.Mention} was not found. Have they been on the server before?");
                    return;
                }

                userLevel.IsXpIgnored = true;
                await this.userLevelService.SaveAsync(userLevel);
                await ctx.ReplyAsync(CybermancyColor.Green, message: $"{user.Mention} is now ignored for xp gain.");
            }

            if (snowflake is DiscordChannel discordChannel)
            {
                var channel = await this.channelService.GetChannelAsync(discordChannel);
                if (channel is null)
                {
                    await ctx.ReplyAsync(CybermancyColor.Orange, message: $"{discordChannel.Mention} was not found.");
                    return;
                }

                channel.IsXpIgnored = true;
                await this.channelService.SaveAsync(channel);
                await ctx.ReplyAsync(CybermancyColor.Green, message: $"{discordChannel.Mention} is now ignored for xp gain.");
            }

            if (snowflake is DiscordRole discordRole)
            {
                var role = await this.roleService.GetRoleAsync(discordRole, ctx.Guild);
                if (role is null)
                {
                    await ctx.ReplyAsync(CybermancyColor.Orange, message: $"{discordRole.Mention} was not found.");
                    return;
                }

                role.IsXpIgnored = true;
                await this.roleService.SaveAsync(role);
                await ctx.ReplyAsync(CybermancyColor.Green, message: $"{discordRole.Mention} is now ignored for xp gain.");
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="snowflake"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [SlashCommand("Watch", "Watches a perviously ignored user, channel, or role for xp gains")]
        public async Task WatchAsync(InteractionContext ctx, [Option("Item", "The user, channel or role to Observe")] SnowflakeObject snowflake)
        {
            if (snowflake is DiscordUser user)
            {
                var userLevel = await this.userLevelService.GetUserLevelAsync(user.Id, ctx.Guild.Id);
                if (userLevel is null)
                {
                    await ctx.ReplyAsync(CybermancyColor.Orange, message: $"{user.Mention} was not found. Have they been on the server before?");
                    return;
                }

                userLevel.IsXpIgnored = false;
                await this.userLevelService.SaveAsync(userLevel);
                await ctx.ReplyAsync(CybermancyColor.Green, message: $"{ctx.Guild.CurrentMember.DisplayName} is now watching {user.Mention} for xp again.");
            }

            if (snowflake is DiscordChannel discordChannel)
            {
                var channel = await this.channelService.GetChannelAsync(discordChannel);
                if (channel is null)
                {
                    await ctx.ReplyAsync(CybermancyColor.Orange, message: $"{discordChannel.Mention} was not found.");
                    return;
                }

                channel.IsXpIgnored = false;
                await this.channelService.SaveAsync(channel);
                await ctx.ReplyAsync(CybermancyColor.Green, message: $"{ctx.Guild.CurrentMember.DisplayName} is now watching {discordChannel.Mention} for xp again.");
            }

            if (snowflake is DiscordRole discordRole)
            {
                var role = await this.roleService.GetRoleAsync(discordRole, ctx.Guild);
                if (role is null)
                {
                    await ctx.ReplyAsync(CybermancyColor.Orange, message: $"{discordRole.Mention} was not found.");
                    return;
                }

                role.IsXpIgnored = false;
                await this.roleService.SaveAsync(role);
                await ctx.ReplyAsync(CybermancyColor.Green, message: $"{ctx.Guild.CurrentMember.DisplayName} is now watching {discordRole.Mention} for xp again.");
            }
        }

        // [SlashCommand("ShowIgnored", "Shows all currently ignored for the server.")]
        // public async Task ShowIgnoredAsync(InteractionContext ctx)
        // {
        //    var channels = await this.channelService.GetAllIgnoredChannelsAsync(ctx.Guild.Id);
        //    var roles = await this.roleService.GetAllIgnoredRolesAsync(ctx.Guild.Id);
        //    var users = await this.userLevelService.GetAllIgnoredUsersAsync(ctx.Guild.Id);
        //    if (!channels.Any() && !roles.Any() && !users.Any())
        //    {
        //        await ctx.ReplyAsync(CybermancyColor.Orange, message: "This server does not have any ignored channels, roles or users.");
        //        return;
        //    }
        // }
    }
}
