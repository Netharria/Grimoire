// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Leveling.Events;

public sealed partial class GainUserXp
{
    public sealed partial class EventHandler(IMediator mediator) : IEventHandler<MessageCreatedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs args)
        {
            if (args.Message.MessageType is not DiscordMessageType.Default and not DiscordMessageType.Reply
                || args.Author is not DiscordMember member)
                return;
            if (member.IsBot)
                return;
            var response = await this._mediator.Send(new Request
            {
                ChannelId = args.Channel.Id,
                GuildId = args.Guild.Id,
                UserId = member.Id,
                RoleIds = member.Roles.Select(x => x.Id).ToArray()
            });
            if (!response.EarnedXp)
                return;

            await this._mediator.Publish(new UserGainedXpEvent
            {
                GuildId = args.Guild.Id,
                UserId = member.Id,
                UserLevel = response.CurrentLevel
            });

            if (response.LevelLogChannel is null)
                return;

            if (!args.Guild.Channels
                .TryGetValue(response.LevelLogChannel.Value, out var loggingChannel))
                return;

            if (response.PreviousLevel < response.CurrentLevel)
                await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithColor(GrimoireColor.Purple)
                    .WithAuthor(member.GetUsernameWithDiscriminator())
                    .WithDescription($"{member.Mention} has leveled to level {response.CurrentLevel}.")
                    .WithFooter($"{member.Id}")
                    .WithTimestamp(DateTime.UtcNow)
                    .Build());


        }
    }

    public sealed record UserGainedXpEvent : INotification
    {
        public required ulong GuildId { get; init; }
        public required ulong UserId { get; init; }
        public required int UserLevel { get; init; }
    }

    public sealed record Request : IRequest<Response>
    {
        public required ulong GuildId { get; init; }
        public required ulong UserId { get; init; }
        public required ulong ChannelId { get; init; }
        public required ulong[] RoleIds { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Request, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;
        public async ValueTask<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            var result = await this._grimoireDbContext.Members
            .AsNoTracking()
            .WhereLevelingEnabled()
            .WhereMemberHasId(command.UserId, command.GuildId)
            .WhereMemberNotIgnored(command.ChannelId, command.RoleIds)
            .Select(x => new
            {
                Xp = x.XpHistory.Sum(x => x.Xp),
                Timeout = x.XpHistory.Select(x => x.TimeOut)
                    .OrderByDescending(x => x)
                    .FirstOrDefault(),
                x.Guild.LevelSettings.Base,
                x.Guild.LevelSettings.Modifier,
                x.Guild.LevelSettings.Amount,
                x.Guild.LevelSettings.LevelChannelLogId,
                x.Guild.LevelSettings.TextTime
            }).FirstOrDefaultAsync(cancellationToken);

            if (result is null || result.Timeout > DateTimeOffset.UtcNow)
                return new Response { };

            await this._grimoireDbContext.XpHistory.AddAsync(new XpHistory
            {
                Xp = result.Amount,
                UserId = command.UserId,
                GuildId = command.GuildId,
                TimeOut = DateTimeOffset.UtcNow + result.TextTime,
                Type = XpHistoryType.Earned
            }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            return new Response
            {
                EarnedXp = true,
                PreviousLevel = MemberExtensions.GetLevel(result.Xp, result.Base, result.Modifier),
                CurrentLevel = MemberExtensions.GetLevel(result.Xp + result.Amount, result.Base, result.Modifier),
                LevelLogChannel = result.LevelChannelLogId
            };

        }
    }

    public sealed record Response : BaseResponse
    {
        public int PreviousLevel { get; init; }
        public int CurrentLevel { get; init; }
        public ulong? LevelLogChannel { get; init; }
        public bool EarnedXp { get; init; }
    }
}

