// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;
using static Grimoire.Features.Leveling.Events.GainUserXp;

namespace Grimoire.Features.Leveling.Events;

public partial class CheckIfUserEarnedReward
{
    public partial class NotificationHandler(IMediator mediator, DiscordClient client, ILogger<NotificationHandler> logger) : INotificationHandler<UserGainedXpEvent>
    {
        private readonly IMediator _mediator = mediator;
        private readonly DiscordClient _client = client;
        private readonly ILogger<NotificationHandler> _logger = logger;

        public async ValueTask Handle(UserGainedXpEvent notification, CancellationToken cancellationToken)
        {
            var guild = await this._client.GetGuildAsync(notification.GuildId);
            var member = await guild.GetMemberAsync(notification.UserId);

            var response = await this._mediator.Send(new Request
            {
                GuildId = notification.GuildId,
                UserLevel = notification.UserLevel
            }, cancellationToken);

            if (response is null)
                return;

            var newRewards = response.EarnedRewards
            .Where(x => !member.Roles.Any(y => y.Id == x.RoleId))
            .ToArray();

            var rolesToAdd = newRewards
            .Join(guild.Roles, x => x.RoleId, y => y.Key, (x, y) => y.Value)
            .Concat(member.Roles)
            .Distinct()
            .ToArray();

            if (newRewards.Length > 0)
            {
                try
                {
                    await member.ReplaceRolesAsync(rolesToAdd);
                }
                catch (UnauthorizedException)
                {
                    await SendErrorLogs(
                        guild.Channels,
                        guild.CurrentMember.DisplayName,
                        newRewards.Select(x => x.RoleId).ToArray(),
                        response.LogChannelId,
                        response.LevelLogChannel);
                }
                foreach (var reward in newRewards.Where(x => !string.IsNullOrWhiteSpace(x.Message)))
                {
                    try
                    {
                        if (guild.Roles.TryGetValue(reward.RoleId, out var role))
                        {
                            await member.SendMessageAsync(new DiscordEmbedBuilder()
                                .WithAuthor($"Congratulations on earning {role.Name}!", iconUrl: guild.IconUrl)
                                .WithFooter($"Message from the moderators of {guild.Name}.")
                                .WithDescription(Regex.Unescape(reward!.Message!)));
                        }

                    }
                    catch (Exception ex)
                    {
                        LogRewardMessageFailure(this._logger, ex, reward.RoleId, reward.Message);
                    }
                }
                if (response.LevelLogChannel is null)
                    return;

                if (!guild.Channels
                    .TryGetValue(response.LevelLogChannel.Value, out var loggingChannel))
                    return;
                await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithColor(GrimoireColor.DarkPurple)
                    .WithAuthor(member.GetUsernameWithDiscriminator())
                    .WithDescription($"{member.Mention} has earned " +
                    $"{string.Join(' ', newRewards.Select(x => RoleExtensions.Mention(x.RoleId)))}")
                    .WithFooter($"{member.Id}")
                    .WithTimestamp(DateTime.UtcNow)
                    .Build());
            }


        }

        [LoggerMessage(LogLevel.Warning, "Failure to send reward message Reward: {roleId} Message: {message}")]
        static partial void LogRewardMessageFailure(ILogger logger, Exception ex, ulong roleId, string? message);

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

    public sealed record Request : IRequest<Response?>
    {
        public required ulong GuildId { get; init; }
        public required int UserLevel { get; init; }
    }

    public sealed class RequestHandler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Request, Response?>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response?> Handle(Request request, CancellationToken cancellationToken)
            => await this._grimoireDbContext.Guilds
                .Where(guild => guild.Id == request.GuildId)
                .Select(guild => new Response
                {
                    LogChannelId = guild.ModChannelLog,
                    LevelLogChannel = guild.LevelSettings.LevelChannelLogId,
                    EarnedRewards = guild.Rewards
                        .Where(reward => reward.RewardLevel <= request.UserLevel)
                        .Select(reward => new RewardDto
                        {
                            Message = reward.RewardMessage,
                            RoleId = reward.RoleId
                        })
                }).FirstOrDefaultAsync(cancellationToken);
    }

    public sealed record Response : BaseResponse
    {
        public required IEnumerable<RewardDto> EarnedRewards { get; init; }
        public required ulong? LevelLogChannel { get; init; }
    }

    public sealed record RewardDto
    {
        public ulong RoleId { get; init; }
        public string? Message { get; init; }
    }
}

