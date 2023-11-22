// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using DSharpPlus.Exceptions;
using Grimoire.Core.Features.Leveling.Commands;
using Serilog;

namespace Grimoire.Discord.LevelingModule;

[DiscordMessageCreatedEventSubscriber]
public class LevelingEvents(IMediator mediator, ILogger logger) : IDiscordMessageCreatedEventSubscriber
{
    private readonly IMediator _mediator = mediator;
    private readonly ILogger _logger = logger;

    public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
    {
        if (args.Message.MessageType is not MessageType.Default and not MessageType.Reply ||
            args.Author is not DiscordMember member) return;
        if (member.IsBot) return;
        var response = await this._mediator.Send(new GainUserXpCommand
        {
            ChannelId = args.Channel.Id,
            GuildId = args.Guild.Id,
            UserId = member.Id,
            RoleIds = member.Roles.Select(x => x.Id).ToArray()
        });
        if (!response.Success) return;

        var newRewards = response.EarnedRewards
            .Where(x => !member.Roles.Any(y => y.Id == x.RoleId))
            .ToArray();

        var rolesToAdd = newRewards
            .Join(args.Guild.Roles, x => x.RoleId, y => y.Key, (x, y) => y.Value)
            .Concat(member.Roles)
            .Distinct()
            .ToArray();

        if (rolesToAdd.Except(member.Roles).Any())
        {
            try
            {
                await member.ReplaceRolesAsync(rolesToAdd);
            }
            catch (UnauthorizedException)
            {
                await SendErrorLogs(
                    args.Guild.Channels,
                    args.Guild.CurrentMember.DisplayName,
                    newRewards.Select(x => x.RoleId).ToArray(),
                    response.LogChannelId,
                    response.LevelLogChannel);
            }
            foreach (var reward in newRewards.Where(x => !string.IsNullOrWhiteSpace(x.Message)))
            {
                try
                {
                    if (args.Guild.Roles.TryGetValue(reward.RoleId, out var role))
                    {
                        await member.SendMessageAsync(new DiscordEmbedBuilder()
                            .WithAuthor($"Congratulations on earning {role.Name}!", iconUrl: args.Guild.IconUrl)
                            .WithFooter($"Message from the moderators of {args.Guild.Name}.")
                            .WithDescription(Regex.Unescape(reward!.Message!)));
                    }

                }
                catch (Exception ex)
                {
                    this._logger.Warning("Failure to send reward message Reward: {roleId} Message: {message}", reward.RoleId, reward.Message, ex);
                }
            }
        }

        if (response.LevelLogChannel is null) return;

        if (!args.Guild.Channels.TryGetValue(response.LevelLogChannel.Value,
            out var loggingChannel)) return;

        if (response.PreviousLevel < response.CurrentLevel)
            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithColor(GrimoireColor.Purple)
                .WithAuthor(member.GetUsernameWithDiscriminator())
                .WithDescription($"{member.Mention} has leveled to level {response.CurrentLevel}.")
                .WithFooter($"{member.Id}")
                .WithTimestamp(DateTime.UtcNow)
                .Build());

        if (newRewards.Length != 0)
            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithColor(GrimoireColor.DarkPurple)
                .WithAuthor($"{member.Username}#{member.Discriminator}")
                .WithDescription($"{member.Mention} has earned " +
                $"{string.Join(' ', newRewards.Select(x => RoleExtensions.Mention(x.RoleId)))}")
                .WithFooter($"{member.Id}")
                .WithTimestamp(DateTime.UtcNow)
                .Build());
    }

    private static async Task SendErrorLogs(
        IReadOnlyDictionary<ulong, DiscordChannel> channels,
        string displayName,
        ulong[] rewards,
        ulong? modLogChannelId,
        ulong? levelLogChannelId)
    {
        if (modLogChannelId is not null)
        {
            if (channels.TryGetValue(modLogChannelId.Value, out var modLogChannel))
                await modLogChannel.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithColor(GrimoireColor.Red)
                    .WithDescription($"{displayName} tried to grant roles " +
                    $"{string.Join(' ', rewards.Select(RoleExtensions.Mention))} but did not have sufficent permissions."));
        }
        if (levelLogChannelId is not null)
        {
            if (channels.TryGetValue(levelLogChannelId.Value, out var levelLogChannel))
                await levelLogChannel.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithColor(GrimoireColor.Red)
                    .WithDescription($"{displayName} tried to grant roles " +
                    $"{string.Join(' ', rewards.Select(RoleExtensions.Mention))} but did not have sufficent permissions."));
        }
    }
}
