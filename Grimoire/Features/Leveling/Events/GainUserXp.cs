// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Leveling.Events;

public sealed partial class GainUserXp
{
    public sealed partial class EventHandler(IMediator mediator) : IEventHandler<MessageCreatedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs args)
        {
            if (args.Message.MessageType is not DiscordMessageType.Default and not DiscordMessageType.Reply
                || args.Author is not DiscordMember member
                || member.IsBot)
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

            if (response.PreviousLevel < response.CurrentLevel)
                await sender.SendMessageToLoggingChannel(response.LevelLogChannel, new DiscordEmbedBuilder()
                    .WithColor(GrimoireColor.Purple)
                    .WithAuthor(member.GetUsernameWithDiscriminator())
                    .WithDescription($"{member.Mention} has leveled to level {response.CurrentLevel}.")
                    .WithFooter($"{member.Id}")
                    .WithTimestamp(DateTime.UtcNow));


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

        private static readonly Func<GrimoireDbContext, ulong, ulong, ulong, ulong[], IAsyncEnumerable<QueryResult>>
            _getUserXpInfoQuery = EF.CompileAsyncQuery((GrimoireDbContext context, ulong userId, ulong guildId, ulong channelId, ulong[] roleIds) =>
                context.Members
                .AsNoTracking()
                .Where(member => member.Guild.LevelSettings.ModuleEnabled)
                .Where(member => member.UserId == userId)
                .Where(member => member.GuildId == guildId)
                .Where(member => member.IsIgnoredMember == null)
                .Where(member => member.Guild.IgnoredChannels.All(x => x.ChannelId != channelId))
                .Where(member => !member.Guild.IgnoredRoles.Any(x => roleIds.Contains(x.RoleId)))
                .Select(member => new QueryResult
                {
                    Xp = member.XpHistory.Sum(xpHistory => xpHistory.Xp),
                    Timeout = member.XpHistory
                        .Select(xpHistory => xpHistory.TimeOut)
                        .Max(dateTimeOffset => dateTimeOffset),
                    Base = member.Guild.LevelSettings.Base,
                    Modifier = member.Guild.LevelSettings.Modifier,
                    Amount = member.Guild.LevelSettings.Amount,
                    LevelChannelLogId = member.Guild.LevelSettings.LevelChannelLogId,
                    TextTime = member.Guild.LevelSettings.TextTime
                }).Take(1));

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            var result = await _getUserXpInfoQuery(this._grimoireDbContext,
                    command.UserId,
                    command.GuildId,
                    command.ChannelId,
                    command.RoleIds)
                .FirstOrDefaultAsync(cancellationToken);

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
    public sealed record QueryResult
    {
        public required long Xp { get; init; }
        public required DateTimeOffset Timeout { get; init; }
        public required int Base { get; init; }
        public required int Modifier {  get; init; }
        public required int Amount { get; init; }
        public ulong? LevelChannelLogId { get; init; }
        public required TimeSpan TextTime { get; init; }
    }
}
