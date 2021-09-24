// -----------------------------------------------------------------------
// <copyright file="LevelingEvents.cs" company="Netharia">
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
    using DSharpPlus.EventArgs;
    using Nefarius.DSharpPlus.Extensions.Hosting.Attributes;
    using Nefarius.DSharpPlus.Extensions.Hosting.Events;

    [DiscordMessageEventsSubscriber]
    public class LevelingEvents : IDiscordMessageEventsSubscriber
    {
        private readonly IChannelService channelService;
        private readonly IRoleService roleService;
        private readonly IUserLevelService userLevelService;
        private readonly IRewardService rewardService;
        private readonly ILevelSettingsService levelSettingsService;
        private readonly IUserService userService;

        /// <summary>
        /// Initializes a new instance of the <see cref="LevelingEvents"/> class.
        /// </summary>
        /// <param name="channelService"></param>
        /// <param name="roleService"></param>
        /// <param name="userLevelService"></param>
        /// <param name="rewardService"></param>
        /// <param name="levelSettingsService"></param>
        /// <param name="userService"></param>
        public LevelingEvents(IChannelService channelService, IRoleService roleService, IUserLevelService userLevelService, IRewardService rewardService, ILevelSettingsService levelSettingsService, IUserService userService)
        {
            this.channelService = channelService;
            this.roleService = roleService;
            this.userLevelService = userLevelService;
            this.rewardService = rewardService;
            this.levelSettingsService = levelSettingsService;
            this.userService = userService;
        }

        public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
        {
            if (args.Message.MessageType is not MessageType.Default or MessageType.Reply) return;
            if (args.Author is not DiscordMember member) return;
            if (member.IsBot) return;
            if (await this.channelService.IsChannelIgnoredAsync(args.Channel)) return;
            if (!await this.levelSettingsService.IsLevelingEnabledAsync(member.Guild.Id)) return;
            if (await this.roleService.AreAnyRolesIgnoredAsync(member.Roles.ToList(), member.Guild)) return;
            var user = await this.userService.GetUserAsync(member);
            var userLevel = await this.userLevelService.GetUserLevelAsync(member.Id, member.Guild.Id);
            if (userLevel.IsXpIgnored) return;
            if (userLevel.TimeOut > DateTime.UtcNow) return;
            var previousLevel = userLevel.GetLevel();
            userLevel.GrantXp();
            await this.userLevelService.SaveAsync(userLevel);
            var rewardsGranted = await this.rewardService.GrantRewardsMissingFromUserAsync(member);
            if (previousLevel < userLevel.GetLevel())
            {
                await this.levelSettingsService.SendLevelingLogAsync(
                    member.Guild.Id,
                    CybermancyColor.Purple,
                    title: $"{member.Username}#{member.Discriminator}",
                    message: $"{member.Mention} has leveled to level {userLevel.GetLevel()}.",
                    footer: $"{member.Id}");
            }

            if (rewardsGranted.Any())
            {
                await this.levelSettingsService.SendLevelingLogAsync(
                    member.Guild.Id,
                    CybermancyColor.Gold,
                    title: $"{member.Username}#{member.Discriminator}",
                    message: $"{member.Mention} has earned {rewardsGranted.Select(x => x.Mention)}",
                    footer: $"{member.Id}");
            }
        }

        #region UnusedEvents
        public Task DiscordOnMessageAcknowledged(DiscordClient sender, MessageAcknowledgeEventArgs args)
        {
            return Task.CompletedTask;
        }

        public Task DiscordOnMessageUpdated(DiscordClient sender, MessageUpdateEventArgs args)
        {
            return Task.CompletedTask;
        }

        public Task DiscordOnMessageDeleted(DiscordClient sender, MessageDeleteEventArgs args)
        {
            return Task.CompletedTask;
        }

        public Task DiscordOnMessagesBulkDeleted(DiscordClient sender, MessageBulkDeleteEventArgs args)
        {
            return Task.CompletedTask;
        }
        #endregion
    }
}