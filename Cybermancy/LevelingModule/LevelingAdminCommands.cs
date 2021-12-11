// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Enums;
using Cybermancy.Core.Features.Leveling.Commands.AwardUserXp;
using Cybermancy.Core.Features.Leveling.Commands.ReclaimUserXp;
using Cybermancy.Core.Features.Leveling.Commands.Shared;
using Cybermancy.Core.Features.Leveling.Commands.UpdateIgnoreStateForXpGain;
using Cybermancy.Core.Features.Leveling.Queries.GetIgnoredItems;
using Cybermancy.Enums;
using Cybermancy.Extensions;
using Cybermancy.SlashCommandAttributes;
using Cybermancy.Utilities;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using MediatR;

namespace Cybermancy.LevelingModule
{
    [SlashRequireGuild]
    [SlashRequireUserPermissions(Permissions.ManageMessages)]
    [SlashRequireModuleEnabled(Module.Leveling)]
    public class LevelingAdminCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;
        private readonly DiscordClient _client;

        public LevelingAdminCommands(IMediator mediator, DiscordClient client)
        {
            this._mediator = mediator;
            this._client = client;
        }

        [SlashCommand("Award", "Awards a user some xp.")]
        public async Task AwardAsync(InteractionContext ctx, [Option("User", "User to award xp.")] DiscordUser user, [Option("XP", "The amount of xp to grant.")] long xpToAward)
        {
            var response = await _mediator.Send(new AwardUserXpCommand{ UserId = user.Id, GuildId = ctx.Guild.Id, XpToAward = xpToAward});

            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }

            await ctx.ReplyAsync(CybermancyColor.Gold, message: $"{user.Mention} has been awarded {xpToAward} xp.", ephemeral: false);
        }


        [SlashCommand("Reclaim", "Takes away xp from user.")]
        public async Task ReclaimAsync(InteractionContext ctx, [Option("User", "User to take xp away from.")] DiscordUser user, [Option("XP", "The amount of xp to Take.")] string amount)
        {
            var response = await _mediator.Send(new ReclaimUserXpCommand{ UserId = user.Id, GuildId = ctx.Guild.Id, XpToTake = amount});

            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }

            await ctx.ReplyAsync(CybermancyColor.Gold, message: $"{amount} xp has been taken from {user.Mention}.", ephemeral: false);
        }

        [SlashCommand("ShowIgnored", "Shows all currently ignored for the server.")]
        public async Task ShowIgnoredAsync(InteractionContext ctx)
        {

            var response = await this._mediator.Send(new GetIgnoredItemsQuery { GuildId = ctx.Guild.Id });

            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }

            var interactivity = ctx.Client.GetInteractivity();
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Ignored Channels Roles and Users.")
                .WithTimestamp(DateTime.UtcNow);
            var embedPages = interactivity.GeneratePagesInEmbed(input: response.ResultString, splittype: SplitType.Line, embed);
            await interactivity.SendPaginatedResponseAsync(interaction: ctx.Interaction, ephemeral: false, user: ctx.User, pages: embedPages);

        }

        [SlashCommand("Ignore", "Ignores a user, channel, or role for xp gains")]
        public Task IgnoreAsync(InteractionContext ctx, [Option("items", "The users, channels or roles to ignore")] string value) =>
            this.UpdateIgnoreState(ctx, value, true);

        [SlashCommand("Watch", "Watches a perviously ignored user, channel, or role for xp gains")]
        public Task WatchAsync(InteractionContext ctx, [Option("Item", "The user, channel or role to Observe")] string value) =>
            this.UpdateIgnoreState(ctx, value, false);

        private async Task UpdateIgnoreState(InteractionContext ctx, string value, bool shouldIgnore)
        {
            var matchedIds = DiscordSnowflakeParser.ParseStringIntoIdsAndGroupByType(ctx, value);
            if (!matchedIds.Any() || (matchedIds.ContainsKey("Invalid") && matchedIds.Keys.Count == 1))
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: $"Could not parse any ids from the submited values.");
                return;
            }

            var userDtos = matchedIds["Users"]
                .Select(x => this.BuildUserDto(x, ctx.Guild.Id))
                .Select(x => x.Result)
                .OfType<UserDto>().ToList();

            var response = await _mediator.Send(new UpdateIgnoreStateForXpGainCommand
            {
                Users = userDtos,
                RoleIds = matchedIds["Role"].Select(x => ulong.Parse(x)).ToArray(),
                ChannelIds = matchedIds["Channel"].Select(x => ulong.Parse(x)).ToArray(),
                InvalidIds = matchedIds["Invalid"],
                GuildId = ctx.Guild.Id,
                ShouldIgnore = shouldIgnore
            });

            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }

            await ctx.ReplyAsync(CybermancyColor.Green, message: response.Result, ephemeral: false);
        }

        private async Task<UserDto?> BuildUserDto(string idString, ulong guildId)
        {
            var id = ulong.Parse(idString);
            if (_client.Guilds[guildId].Members.TryGetValue(id, out var member))
                return new UserDto
                {
                    Id = member.Id,
                    AvatarUrl = member.AvatarUrl,
                    DisplayName = member.DisplayName,
                    UserName = $"{member.Username}${member.Discriminator}"
                };
            try
            {
                var user = await _client.GetUserAsync(id);
                if (user is not null)
                    return new UserDto
                    {
                        Id = user.Id,
                        AvatarUrl = user.AvatarUrl,
                        DisplayName = user.Username,
                        UserName = $"{user.Username}${user.Discriminator}"
                    };
            }
            catch (Exception)
            {
                return null;
            }
            return null;
        }
    }
}
