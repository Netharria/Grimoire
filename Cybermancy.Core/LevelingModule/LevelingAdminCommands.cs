using Cybermancy.Core.Contracts.Services;
using Cybermancy.Core.Enums;
using Cybermancy.Core.Extensions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Cybermancy.Core.LevelingModule
{
    [SlashRequireGuild]
    [SlashRequireUserPermissions(Permissions.ManageMessages)]
    public class LevelingAdminCommands : ApplicationCommandModule
    {
        private readonly IUserLevelService _userLevelService;
        private readonly IChannelService _channelService;
        private readonly IRoleService _roleService;

        public LevelingAdminCommands(IUserLevelService userLevelService, IChannelService channelService, IRoleService roleService)
        {
            _userLevelService = userLevelService;
            _channelService = channelService;
            _roleService = roleService;
        }

        [SlashCommand("Award", "Awards a user some xp.")]
        public async Task Award(InteractionContext ctx, [Option("User", "User to award xp.")] DiscordUser user, [Option("XP", "The amount of xp to grant.")] int amount)
        {
            if (amount <= 0)
            {
                await ctx.Reply(CybermancyColor.Orange, message: "XP needs to be a positive value");
                return;
            }
            var userLevel = await _userLevelService.GetUserLevel(user.Id, ctx.Guild.Id);
            if(userLevel is null)
            {
                await ctx.Reply(CybermancyColor.Orange, message: $"{user.Mention} was not found. Have they been on the server before?");
                return;
            }
            userLevel.GrantXp(amount);
            await _userLevelService.Save(userLevel);
            await ctx.Reply(CybermancyColor.Gold, message: $"{user.Mention} has been awarded{amount} xp.");
        }

        [SlashCommand("Reclaim", "Takes away xp from user.")]
        public async Task Reclaim(InteractionContext ctx, [Option("User", "User to take xp away from.")] DiscordUser user, [Option("XP", "The amount of xp to Take.")] string amount)
        {
            var userLevel = await _userLevelService.GetUserLevel(user.Id, ctx.Guild.Id);
            if (userLevel is null)
            {
                await ctx.Reply(CybermancyColor.Orange, message: $"{user.Mention} was not found. Have they been on the server before?");
                return;
            }
            var xpToTake = 0;
            if (amount.Equals("All", StringComparison.CurrentCultureIgnoreCase))
                xpToTake = userLevel.Xp;
            else if(!int.TryParse(amount, out xpToTake))
            {
                await ctx.Reply(CybermancyColor.Orange, message: $"XP needs to be a valid number.");
                return;
            }
            if (xpToTake <= 0)
            {
                await ctx.Reply(CybermancyColor.Orange, message: "XP needs to be a positive value.");
                return;
            }
            
            userLevel.Xp -= xpToTake;
            await _userLevelService.Save(userLevel);
            await ctx.Reply(CybermancyColor.Gold, message: $"{amount} xp has been taken from {user.Mention}.");
        }

        [SlashCommand("Ignore", "Ignores a user, channel, or role for xp gains")]
        public async Task Ignore(InteractionContext ctx, [Option("Item", "The user, channel or role to ignore")] SnowflakeObject snowflake)
        {
            if(snowflake is DiscordUser user)
            {
                var userLevel = await _userLevelService.GetUserLevel(user.Id, ctx.Guild.Id);
                if(userLevel is null)
                {
                    await ctx.Reply(CybermancyColor.Orange, message: $"{user.Mention} was not found. Have they been on the server before?");
                    return;
                }
                userLevel.IsXpIgnored = true;
                await _userLevelService.Save(userLevel);
                await ctx.Reply(CybermancyColor.Green, message: $"{user.Mention} is now ignored for xp gain.");
            }
            if(snowflake is DiscordChannel discordChannel)
            {
                var channel = await _channelService.GetChannel(discordChannel);
                if (channel is null)
                {
                    await ctx.Reply(CybermancyColor.Orange, message: $"{discordChannel.Mention} was not found.");
                    return;
                }
                channel.IsXpIgnored = true;
                await _channelService.Save(channel);
                await ctx.Reply(CybermancyColor.Green, message: $"{discordChannel.Mention} is now ignored for xp gain.");
            }
            if(snowflake is DiscordRole discordRole)
            {
                var role = await _roleService.GetRole(discordRole, ctx.Guild);
                if (role is null)
                {
                    await ctx.Reply(CybermancyColor.Orange, message: $"{discordRole.Mention} was not found.");
                    return;
                }
                role.IsXpIgnored = true;
                await _roleService.Save(role);
                await ctx.Reply(CybermancyColor.Green, message: $"{discordRole.Mention} is now ignored for xp gain.");
            }
        }

        [SlashCommand("Watch", "Watches a perviously ignored user, channel, or role for xp gains")]
        public async Task Watch(InteractionContext ctx, [Option("Item", "The user, channel or role to Observe")] SnowflakeObject snowflake)
        {
            if (snowflake is DiscordUser user)
            {
                var userLevel = await _userLevelService.GetUserLevel(user.Id, ctx.Guild.Id);
                if (userLevel is null)
                {
                    await ctx.Reply(CybermancyColor.Orange, message: $"{user.Mention} was not found. Have they been on the server before?");
                    return;
                }
                userLevel.IsXpIgnored = false;
                await _userLevelService.Save(userLevel);
                await ctx.Reply(CybermancyColor.Green, message: $"{ctx.Guild.CurrentMember.DisplayName} is now watching {user.Mention} for xp again.");
            }
            if (snowflake is DiscordChannel discordChannel)
            {
                var channel = await _channelService.GetChannel(discordChannel);
                if (channel is null)
                {
                    await ctx.Reply(CybermancyColor.Orange, message: $"{discordChannel.Mention} was not found.");
                    return;
                }
                channel.IsXpIgnored = false;
                await _channelService.Save(channel);
                await ctx.Reply(CybermancyColor.Green, message: $"{ctx.Guild.CurrentMember.DisplayName} is now watching {discordChannel.Mention} for xp again.");
            }
            if (snowflake is DiscordRole discordRole)
            {
                var role = await _roleService.GetRole(discordRole, ctx.Guild);
                if (role is null)
                {
                    await ctx.Reply(CybermancyColor.Orange, message: $"{discordRole.Mention} was not found.");
                    return;
                }
                role.IsXpIgnored = false;
                await _roleService.Save(role);
                await ctx.Reply(CybermancyColor.Green, message: $"{ctx.Guild.CurrentMember.DisplayName} is now watching {discordRole.Mention} for xp again.");
            }
        }

        [SlashCommand("ShowIgnored", "Shows all currently ignored for the server.")]
        public async Task ShowIgnored(InteractionContext ctx)
        {

            var channels = await _channelService.GetAllIgnoredChannels(ctx.Guild.Id);
            var roles = await _roleService.GetAllIgnoredRoles(ctx.Guild.Id);
            var users = await _userLevelService.GetAllIgnoredUsers(ctx.Guild.Id);
            if(!channels.Any() && !roles.Any() && !users.Any())
            {
                await ctx.Reply(CybermancyColor.Orange, message: "This server does not have any ignored channels, roles or users.");
                return;
            }
        }
    }
}
