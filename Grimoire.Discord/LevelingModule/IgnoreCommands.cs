// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using Grimoire.Core.Features.Leveling.Commands.ManageXpCommands.UpdateIgnoreStateForXpGain;
using Grimoire.Core.Features.Leveling.Queries.GetIgnoredItems;

namespace Grimoire.Discord.LevelingModule
{
    [SlashRequireGuild]
    [SlashRequireUserPermissions(Permissions.ManageGuild)]
    [SlashRequireModuleEnabled(Module.Leveling)]
    [SlashCommandGroup("Ignore", "View or edit who is ignored for xp gain.")]
    public class IgnoreCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public IgnoreCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("View", "View all currently ignored users, channels and roles for the server.")]
        public async Task ShowIgnoredAsync(InteractionContext ctx)
        {

            var response = await this._mediator.Send(new GetIgnoredItemsQuery { GuildId = ctx.Guild.Id });

            var interactivity = ctx.Client.GetInteractivity();
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Ignored Channels Roles and Users.")
                .WithTimestamp(DateTime.UtcNow);
            var embedPages = interactivity.GeneratePagesInEmbed(input: response.Message, splittype: SplitType.Line, embed);
            await interactivity.SendPaginatedResponseAsync(interaction: ctx.Interaction, ephemeral: false, user: ctx.User, pages: embedPages);

        }

        [SlashCommand("Add", "Ignores a user, channel, or role for xp gains")]
        public Task IgnoreAsync(InteractionContext ctx, [Option("items", "The users, channels or roles to ignore")] string value) =>
            this.UpdateIgnoreState(ctx, value, true);

        [SlashCommand("Remove", "Removes a user, channel, or role from the ignored xp list.")]
        public Task WatchAsync(InteractionContext ctx, [Option("Item", "The user, channel or role to Observe")] string value) =>
            this.UpdateIgnoreState(ctx, value, false);

        private async Task UpdateIgnoreState(InteractionContext ctx, string value, bool shouldIgnore)
        {
            var matchedIds = await DiscordSnowflakeParser.ParseStringIntoIdsAndGroupByTypeAsync(ctx, value);
            if (!matchedIds.Any() || (matchedIds.ContainsKey("Invalid") && matchedIds.Keys.Count == 1))
            {
                await ctx.ReplyAsync(GrimoireColor.Orange, message: $"Could not parse any ids from the submited values.");
                return;
            }

            var userDtos = matchedIds.ContainsKey("User") ? await matchedIds["User"]
                .ToAsyncEnumerable()
                .SelectAwait(async x => await BuildUserDto(ctx.Client, x, ctx.Guild.Id))
                .OfType<UserDto>()
                .ToArrayAsync() : Array.Empty<UserDto>();

            var response = await this._mediator.Send(new UpdateIgnoreStateForXpGainCommand
            {
                Users = userDtos,
                Roles = matchedIds.ContainsKey("Role") ? matchedIds["Role"]
                    .Select(x =>
                        new RoleDto
                        {
                            Id = ulong.Parse(x),
                            GuildId = ctx.Guild.Id
                        }).ToArray() : Array.Empty<RoleDto>(),
                Channels = matchedIds.ContainsKey("Channel") ? matchedIds["Channel"]
                    .Select(x =>
                        new ChannelDto
                        {
                            Id = ulong.Parse(x),
                            GuildId = ctx.Guild.Id
                        }).ToArray() : Array.Empty<ChannelDto>(),
                InvalidIds = matchedIds.ContainsKey("Invalid") ? matchedIds["Invalid"] : Array.Empty<string>(),
                GuildId = ctx.Guild.Id,
                ShouldIgnore = shouldIgnore
            });

            await ctx.ReplyAsync(GrimoireColor.Green, message: response.Message, ephemeral: false);
            await ctx.SendLogAsync(response, GrimoireColor.Gold);
        }

        private async static ValueTask<UserDto?> BuildUserDto(DiscordClient client, string idString, ulong guildId)
        {
            var id = ulong.Parse(idString);
            if (client.Guilds[guildId].Members.TryGetValue(id, out var member))
                return new UserDto
                {
                    Id = member.Id,
                    Nickname = member.Nickname,
                    UserName = member.GetUsernameWithDiscriminator()
                };
            try
            {
                var user = await client.GetUserAsync(id);
                if (user is not null)
                    return new UserDto
                    {
                        Id = user.Id,
                        UserName = user.GetUsernameWithDiscriminator()
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
