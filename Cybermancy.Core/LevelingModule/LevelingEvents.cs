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

        public LevelingEvents(IChannelService channelService, IRoleService roleService, IUserLevelService userLevelService, IRewardService rewardService, ILevelSettingsService levelSettingsService)
        {
            _channelService = channelService;
            _roleService = roleService;
            _userLevelService = userLevelService;
            _rewardService = rewardService;
            _levelSettingsService = levelSettingsService;
        }

        public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
        {
            if(args.Message.MessageType is not MessageType.Default or MessageType.Reply) return; 
            if (args.Author is not DiscordMember member) return;
            if(member.IsBot) return;
            if (await _channelService.IsChannelIgnored(args.Channel)) return;
            if(await _levelSettingsService.IsLevelingEnabled(member.Guild.Id)) return;
            if(_roleService.AreAnyRolesIgnored(member.Roles.ToList(), member.Guild)) return;
            var userLevel = await _userLevelService.GetUserLevels(member.Id, member.Guild.Id);
            if(userLevel.IsXpIgnored) return;
            if(userLevel.TimeOut > DateTime.UtcNow) return;
            var previousLevel = userLevel.GetLevel();
            userLevel.GrantXp();
            await _userLevelService.Save(userLevel);
            var rewardsGranted = await _rewardService.GrantRewardsMissingFromUser(member);
            if (previousLevel < userLevel.GetLevel())
            {
                await _levelSettingsService.SendLevelingLog(
                    member.Guild.Id, CybermancyColor.Purple,
                    title: $"{member.Username}#{member.Discriminator}",
                    message: $"{member.Mention} has leveled to level {userLevel.GetLevel()}.",
                    footer: $"{member.Id}"
                );
            }

            if (rewardsGranted.Any())
            {
                await _levelSettingsService.SendLevelingLog(
                    member.Guild.Id, CybermancyColor.Gold,
                    title: $"{member.Username}#{member.Discriminator}",
                    message: $"{member.Mention} has earned {rewardsGranted.Select(x => x.Mention)}",
                    footer: $"{member.Id}"
                );
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