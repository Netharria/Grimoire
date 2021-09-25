// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Core.Enums;
using Cybermancy.Core.Extensions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;

namespace Cybermancy.Core.LevelingModule
{
    [SlashRequireGuild]
    [SlashRequireUserPermissions(Permissions.ManageMessages)]
    public class LevelingAdminCommands : ApplicationCommandModule
    {
        private readonly IUserLevelService _userLevelService;
        private readonly IChannelService _channelService;
        private readonly IRoleService _roleService;

        /// <summary>
        /// Initializes a new instance of the <see cref="LevelingAdminCommands"/> class.
        /// </summary>
        /// <param name="userLevelService"></param>
        /// <param name="channelService"></param>
        /// <param name="roleService"></param>
        public LevelingAdminCommands(IUserLevelService userLevelService, IChannelService channelService, IRoleService roleService)
        {
            this._userLevelService = userLevelService;
            this._channelService = channelService;
            this._roleService = roleService;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="user"></param>
        /// <param name="amount"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [SlashCommand("Award", "Awards a user some xp.")]
        public async Task AwardAsync(InteractionContext ctx, [Option("User", "User to award xp.")] DiscordUser user, [Option("XP", "The amount of xp to grant.")] string amount)
        {
            if (!int.TryParse(amount, out var xpToGrant))
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: $"XP needs to be a valid number.");
                return;
            }
            if (xpToGrant < 0)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: "XP needs to be a positive value");
                return;
            }

            var userLevel = await this._userLevelService.GetOrCreateUserLevelAsync(user, ctx.Guild.Id);
            if (userLevel is null)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: $"{user.Mention} was not found. Have they been on the server before?");
                return;
            }

            userLevel.GrantXp(xpToGrant);
            await this._userLevelService.SaveAsync(userLevel);
            await ctx.ReplyAsync(CybermancyColor.Gold, message: $"{user.Mention} has been awarded {amount} xp.", ephemeral: false);
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
            var userLevel = await this._userLevelService.GetOrCreateUserLevelAsync(user, ctx.Guild.Id);
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

            if (xpToTake < 0)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: "XP needs to be a positive value.");
                return;
            }
            if (xpToTake > userLevel.Xp) xpToTake = userLevel.Xp;
            userLevel.Xp -= xpToTake;
            await this._userLevelService.SaveAsync(userLevel);
            await ctx.ReplyAsync(CybermancyColor.Gold, message: $"{xpToTake} xp has been taken from {user.Mention}.", ephemeral: false);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="value"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [SlashCommand("Ignore", "Ignores a user, channel, or role for xp gains")]
        public async Task IgnoreAsync(InteractionContext ctx, [Option("items", "The users, channels or roles to ignore")] string value)
        {
            var matches =  Regex.Matches(value, @"(\d{17,21})", RegexOptions.None, TimeSpan.FromSeconds(1));
            if (!matches.Any())
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: $"Could not parse any ids from the submited value.");
                return;
            }
            var newIgnoredItems = new StringBuilder();
            var notFoundInDatabase = new StringBuilder();
            var couldNotMatch = new StringBuilder();
            foreach (Match match in matches)
            {
                if (!ulong.TryParse(match.Value, out var id))
                {
                    couldNotMatch.Append(match).Append(' ');
                    return;
                }
                try
                {
                    var user = await ctx.Client.GetUserAsync(id);
                    var userLevel = await this._userLevelService.GetOrCreateUserLevelAsync(user, ctx.Guild.Id);
                    if (userLevel is null) notFoundInDatabase.Append(user.Mention).Append(' ');

                    userLevel.IsXpIgnored = true;
                    await this._userLevelService.SaveAsync(userLevel);
                    newIgnoredItems.Append(user.Mention).Append(' ');
                    continue;
                }
                catch (NotFoundException) { }

                var discordRole = ctx.Guild.GetRole(id);
                if (discordRole is not null)
                {
                    var role = await this._roleService.GetOrCreateRoleAsync(discordRole, ctx.Guild);
                    if (role is null) notFoundInDatabase.Append(discordRole.Mention).Append(' ');

                    role.IsXpIgnored = true;
                    await this._roleService.SaveAsync(role);
                    newIgnoredItems.Append(discordRole.Mention).Append(' ');
                    continue;
                }

                var discordChannel = ctx.Guild.GetChannel(id);
                if (discordChannel is not null)
                {
                    var channel = await this._channelService.GetOrCreateChannelAsync(discordChannel);
                    if (channel is null) notFoundInDatabase.Append(discordChannel.Mention).Append(' ');

                    channel.IsXpIgnored = true;
                    await this._channelService.SaveAsync(channel);
                    newIgnoredItems.Append(discordChannel.Mention).Append(' ');
                    continue;
                }

            }
            var finalString = new StringBuilder();
            if (notFoundInDatabase.Length > 0) finalString.Append(notFoundInDatabase).Append("were not found in database. ");
            if (couldNotMatch.Length > 0) finalString.Append("Could not match ").Append(couldNotMatch).Append("with a role, channel or user. ");
            if (newIgnoredItems.Length > 0) finalString.Append(newIgnoredItems).Append(" are now ignored for xp gain.");

            await ctx.ReplyAsync(CybermancyColor.Green, message: finalString.ToString(), ephemeral: false);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="snowflake"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [SlashCommand("Watch", "Watches a perviously ignored user, channel, or role for xp gains")]
        public async Task WatchAsync(InteractionContext ctx, [Option("Item", "The user, channel or role to Observe")] string value)
        {
            var matches =  Regex.Matches(value, @"(\d{17,21})", RegexOptions.None, TimeSpan.FromSeconds(1));
            if (!matches.Any())
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: $"Could not parse any ids from the submited value.");
                return;
            }
            var newIgnoredItems = new StringBuilder();
            var notFoundInDatabase = new StringBuilder();
            var couldNotMatch = new StringBuilder();
            foreach (Match match in matches)
            {
                if (!ulong.TryParse(match.Value, out var id))
                {
                    couldNotMatch.Append(match).Append(' ');
                    return;
                }
                try
                {
                    var user = await ctx.Client.GetUserAsync(id);
                    var userLevel = await this._userLevelService.GetOrCreateUserLevelAsync(user, ctx.Guild.Id);
                    if (userLevel is null) notFoundInDatabase.Append(user.Mention).Append(' ');

                    userLevel.IsXpIgnored = false;
                    await this._userLevelService.SaveAsync(userLevel);
                    newIgnoredItems.Append(user.Mention).Append(' ');
                    continue;
                }
                catch (NotFoundException) { }

                var discordRole = ctx.Guild.GetRole(id);
                if (discordRole is not null)
                {
                    var role = await this._roleService.GetOrCreateRoleAsync(discordRole, ctx.Guild);
                    if (role is null) notFoundInDatabase.Append(discordRole.Mention).Append(' ');

                    role.IsXpIgnored = false;
                    await this._roleService.SaveAsync(role);
                    newIgnoredItems.Append(discordRole.Mention).Append(' ');
                    continue;
                }

                var discordChannel = ctx.Guild.GetChannel(id);
                if (discordChannel is not null)
                {
                    var channel = await this._channelService.GetOrCreateChannelAsync(discordChannel);
                    if (channel is null) notFoundInDatabase.Append(discordChannel.Mention).Append(' ');

                    channel.IsXpIgnored = false;
                    await this._channelService.SaveAsync(channel);
                    newIgnoredItems.Append(discordChannel.Mention).Append(' ');
                    continue;
                }
                couldNotMatch.Append(id).Append(' ');
            }
            var finalString = new StringBuilder();
            if (notFoundInDatabase.Length > 0) finalString.Append(notFoundInDatabase).Append("were not found in database. ");
            if (couldNotMatch.Length > 0) finalString.Append("Could not match ").Append(couldNotMatch).Append("with a role, channel or user. ");
            if (newIgnoredItems.Length > 0) finalString.Append(newIgnoredItems).Append(" are now watched for xp gain.");

            await ctx.ReplyAsync(CybermancyColor.Green, message: finalString.ToString(), ephemeral: false);
        }

        [SlashCommand("ShowIgnored", "Shows all currently ignored for the server.")]
        public async Task ShowIgnoredAsync(InteractionContext ctx)
        {
            var channels = await this._channelService.GetAllIgnoredChannelsAsync(ctx.Guild.Id);
            var roles = await this._roleService.GetAllIgnoredRolesAsync(ctx.Guild.Id);
            var users = await this._userLevelService.GetAllIgnoredUsersAsync(ctx.Guild.Id);
            if (!channels.Any() && !roles.Any() && !users.Any())
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: "This server does not have any ignored channels, roles or users.");
                return;
            }
            var ignoredMessageBuilder = new StringBuilder().Append("**Channels**\n");
            foreach (var channel in channels) ignoredMessageBuilder.Append(channel.Mention()).Append('\n');

            ignoredMessageBuilder.Append("\n**Roles**\n");
            foreach (var role in roles) ignoredMessageBuilder.Append(role.Mention()).Append('\n');

            ignoredMessageBuilder.Append("\n**Users**\n");
            foreach (var user in users) ignoredMessageBuilder.Append(user.User.Mention()).Append('\n');

            var interactivity = ctx.Client.GetInteractivity();
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Ignored Channels Roles and Users.")
                .WithTimestamp(DateTime.UtcNow);
            var embedPages = interactivity.GeneratePagesInEmbed(input: ignoredMessageBuilder.ToString(), splittype: SplitType.Line, embed);
            await interactivity.SendPaginatedResponseAsync(interaction: ctx.Interaction, ephemeral: false, user: ctx.User, pages: embedPages);

        }
    }
}
