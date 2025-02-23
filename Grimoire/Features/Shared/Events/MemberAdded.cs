// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Events;

internal sealed class MemberAdded
{
    internal sealed class EventHandler(IMediator mediator) : IEventHandler<GuildMemberAddedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public Task HandleEventAsync(DiscordClient sender, GuildMemberAddedEventArgs eventArgs)
            => this._mediator.Send(
                new Request
                {
                    Nickname =
                        string.IsNullOrWhiteSpace(eventArgs.Member.Nickname) ? null : eventArgs.Member.Nickname,
                    GuildId = eventArgs.Guild.Id,
                    UserId = eventArgs.Member.Id,
                    UserName = eventArgs.Member.GetUsernameWithDiscriminator(),
                    AvatarUrl = eventArgs.Member.GetGuildAvatarUrl(MediaFormat.Auto, 128)
                });
    }

    public sealed record Request : IRequest
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string? Nickname { get; init; }
        public string AvatarUrl { get; init; } = string.Empty;
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task Handle(Request command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var userResult = await dbContext.Users
                .AsNoTracking()
                .Where(x => x.Id == command.UserId)
                .Select(x => new
                {
                    x.UsernameHistories.OrderByDescending(username => username.Timestamp).First().Username
                })
                .FirstOrDefaultAsync(cancellationToken);
            var memberResult = await dbContext.Members
                .AsNoTracking()
                .Where(x => x.UserId == command.UserId && x.GuildId == command.GuildId)
                .Select(x => new
                {
                    x.NicknamesHistory.OrderByDescending(nickname => nickname.Timestamp).First().Nickname,
                    x.AvatarHistory.OrderByDescending(avatar => avatar.Timestamp).First().FileName
                }).FirstOrDefaultAsync(cancellationToken);

            if (userResult is null)
                await dbContext.Users.AddAsync(new User
                {
                    Id = command.UserId,
                    UsernameHistories =
                    [
                        new UsernameHistory { Username = command.UserName, UserId = command.UserId }
                    ]
                }, cancellationToken);

            if (userResult is not null)
                if (!string.Equals(userResult.Username, command.UserName, StringComparison.CurrentCultureIgnoreCase))
                    await dbContext.UsernameHistory.AddAsync(
                        new UsernameHistory { Username = command.UserName, UserId = command.UserId },
                        cancellationToken);

            if (memberResult is null)
            {
                var member = new Member
                {
                    UserId = command.UserId,
                    GuildId = command.GuildId,
                    XpHistory =
                    [
                        new XpHistory
                        {
                            UserId = command.UserId,
                            GuildId = command.GuildId,
                            Type = XpHistoryType.Created,
                            Xp = 0,
                            TimeOut = DateTimeOffset.UtcNow
                        }
                    ],
                    AvatarHistory =
                    [
                        new Avatar { UserId = command.UserId, GuildId = command.GuildId, FileName = command.AvatarUrl }
                    ],
                    NicknamesHistory =
                    [
                        new NicknameHistory
                        {
                            UserId = command.UserId, GuildId = command.GuildId, Nickname = command.Nickname
                        }
                    ]
                };

                await dbContext.Members.AddAsync(member, cancellationToken);
            }

            if (memberResult is not null)
            {
                if (!string.Equals(memberResult.Nickname, command.Nickname, StringComparison.CurrentCultureIgnoreCase))
                    await dbContext.NicknameHistory.AddAsync(
                        new NicknameHistory
                        {
                            UserId = command.UserId, GuildId = command.GuildId, Nickname = command.Nickname
                        }, cancellationToken);

                if (!string.Equals(memberResult.FileName, command.AvatarUrl, StringComparison.Ordinal))
                    await dbContext.Avatars.AddAsync(
                        new Avatar { UserId = command.UserId, GuildId = command.GuildId, FileName = command.AvatarUrl },
                        cancellationToken);
            }

            if (userResult is null
                || memberResult is null
                || !string.Equals(userResult.Username, command.UserName, StringComparison.CurrentCultureIgnoreCase)
                || !string.Equals(memberResult.Nickname, command.Nickname, StringComparison.CurrentCultureIgnoreCase)
                || !string.Equals(memberResult.FileName, command.AvatarUrl, StringComparison.Ordinal))
                await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
