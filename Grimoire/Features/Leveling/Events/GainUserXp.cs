// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.Leveling.Events;

public sealed class GainUserXp
{
    public sealed class EventHandler(IMediator mediator, GuildLog guildLog) : IEventHandler<MessageCreatedEventArgs>
    {
        private readonly IMediator _mediator = mediator;
        private readonly GuildLog _guildLog = guildLog;

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
                GuildId = args.Guild.Id, UserId = member.Id, UserLevel = response.CurrentLevel
            });

            if (response.PreviousLevel < response.CurrentLevel)
                await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
                {
                    GuildId = args.Guild.Id,
                    GuildLogType = GuildLogType.Leveling,
                    Embed = new DiscordEmbedBuilder()
                        .WithColor(GrimoireColor.Purple)
                        .WithAuthor(member.Username)
                        .WithDescription($"{member.Mention} has leveled to level {response.CurrentLevel}.")
                        .WithFooter($"{member.Id}")
                        .WithTimestamp(DateTime.UtcNow)
                });
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

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactoryFactory)
        : IRequestHandler<Request, Response>
    {
        private static readonly Func<GrimoireDbContext, ulong, ulong, ulong, ulong[], Task<QueryResult?>>
            _getUserXpInfoQuery = EF.CompileAsyncQuery(
                (GrimoireDbContext context, ulong userId, ulong guildId, ulong channelId, ulong[] roleIds) =>
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
                            TextTime = member.Guild.LevelSettings.TextTime
                        }).FirstOrDefault());

        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactoryFactory;

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await _getUserXpInfoQuery(dbContext,
                command.UserId,
                command.GuildId,
                command.ChannelId,
                command.RoleIds);

            if (result is null || result.Timeout > DateTimeOffset.UtcNow)
                return new Response();

            await dbContext.XpHistory.AddAsync(
                new XpHistory
                {
                    Xp = result.Amount,
                    UserId = command.UserId,
                    GuildId = command.GuildId,
                    TimeOut = DateTimeOffset.UtcNow + result.TextTime,
                    Type = XpHistoryType.Earned
                }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response
            {
                EarnedXp = true,
                PreviousLevel = MemberExtensions.GetLevel(result.Xp, result.Base, result.Modifier),
                CurrentLevel = MemberExtensions.GetLevel(result.Xp + result.Amount, result.Base, result.Modifier),
            };
        }
    }

    public sealed record Response
    {
        public int PreviousLevel { get; init; }
        public int CurrentLevel { get; init; }
        public bool EarnedXp { get; init; }
    }

    public sealed record QueryResult
    {
        public required long Xp { get; init; }
        public required DateTimeOffset? Timeout { get; init; }
        public required int Base { get; init; }
        public required int Modifier { get; init; }
        public required int Amount { get; init; }
        public required TimeSpan TextTime { get; init; }
    }
}
