// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

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

namespace Cybermancy.Core.LevelingModule
{
    [DiscordMessageEventsSubscriber]
    public class LevelingEvents : IDiscordMessageEventsSubscriber
    {
        private readonly IChannelService _channelService;
        private readonly IRoleService _roleService;
        private readonly IUserLevelService _userLevelService;
        private readonly IRewardService _rewardService;
        private readonly ILevelSettingsService _levelSettingsService;
        private readonly IUserService _userService;

        public LevelingEvents(IChannelService channelService, IRoleService roleService, IUserLevelService userLevelService, IRewardService rewardService, ILevelSettingsService levelSettingsService, IUserService userService)
        {
            this._channelService = channelService;
            this._roleService = roleService;
            this._userLevelService = userLevelService;
            this._rewardService = rewardService;
            this._levelSettingsService = levelSettingsService;
            this._userService = userService;
        }

        public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
        {
            if (args.Message.MessageType is not MessageType.Default or MessageType.Reply) return;
            if (args.Author is not DiscordMember member) return;
            if (member.IsBot) return;
            if (await this._channelService.IsChannelIgnoredAsync(args.Channel)) return;
            if (!await this._levelSettingsService.IsLevelingEnabledAsync(member.Guild.Id)) return;
            if (await this._roleService.AreAnyRolesIgnoredAsync(member.Roles.ToList(), member.Guild)) return;
            var user = await this._userService.GetOrCreateUserAsync(member);
            var userLevel = await this._userLevelService.GetUserLevelAsync(member.Id, member.Guild.Id);
            if (userLevel.IsXpIgnored) return;
            if (userLevel.TimeOut > DateTime.UtcNow) return;
            var previousLevel = userLevel.GetLevel();
            userLevel.GrantXp();
            await this._userLevelService.SaveAsync(userLevel);
            var rewardsGranted = await this._rewardService.GrantRewardsMissingFromUserAsync(member);
            if (previousLevel < userLevel.GetLevel())
            {
                await this._levelSettingsService.SendLevelingLogAsync(
                    member.Guild.Id,
                    CybermancyColor.Purple,
                    title: $"{member.Username}#{member.Discriminator}",
                    message: $"{member.Mention} has leveled to level {userLevel.GetLevel()}.",
                    footer: $"{member.Id}");
            }

            if (rewardsGranted.Any())
            {
                await this._levelSettingsService.SendLevelingLogAsync(
                    member.Guild.Id,
                    CybermancyColor.Gold,
                    title: $"{member.Username}#{member.Discriminator}",
                    message: $"{member.Mention} has earned {rewardsGranted.Select(x => x.Mention)}",
                    footer: $"{member.Id}");
            }
        }

        #region UnusedEvents
        public Task DiscordOnMessageAcknowledged(DiscordClient sender, MessageAcknowledgeEventArgs args) => Task.CompletedTask;

        public Task DiscordOnMessageUpdated(DiscordClient sender, MessageUpdateEventArgs args) => Task.CompletedTask;

        public Task DiscordOnMessageDeleted(DiscordClient sender, MessageDeleteEventArgs args) => Task.CompletedTask;

        public Task DiscordOnMessagesBulkDeleted(DiscordClient sender, MessageBulkDeleteEventArgs args) => Task.CompletedTask;
        #endregion
    }
}
