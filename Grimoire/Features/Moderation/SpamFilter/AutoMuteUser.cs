// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Moderation.SpamFilter;

public sealed class AutoMuteUser
{
    public sealed record Command : IRequest<Response?>
    {
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
        public required ulong ModeratorId { get; init; }
        public required string Reason { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Command command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var response = await dbContext.Members
                .WhereMemberHasId(command.UserId, command.GuildId)
                .Select(x => new
                {
                    x.ActiveMute,
                    x.Guild.ModerationSettings.MuteRole,
                    x.Guild.ModerationSettings.ModuleEnabled,
                    MuteCount = x.UserSins
                        .Where(x => x.SinType == SinType.Mute)
                        .Count(x => x.SinOn > DateTimeOffset.UtcNow.AddDays(-1))
                }).FirstOrDefaultAsync(cancellationToken);
            if (response?.MuteRole is null) return null;
            if (!response.ModuleEnabled) return null;
            if (response.ActiveMute is not null) dbContext.Mutes.Remove(response.ActiveMute);
            var duration = TimeSpan.FromMinutes(Math.Pow(2, response.MuteCount));
            var sin = new Sin
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                ModeratorId = command.ModeratorId,
                Reason = command.Reason,
                SinType = SinType.Mute,
                Mute = new Domain.Mute
                {
                    GuildId = command.GuildId,
                    UserId = command.UserId,
                    EndTime = DateTimeOffset.UtcNow + duration
                }
            };
            await dbContext.Sins.AddAsync(sin, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new Response
            {
                MuteRole = response.MuteRole.Value,
                SinId = sin.Id,
                Duration = duration
            };
        }
    }

    public sealed record Response
    {
        public ulong MuteRole { get; init; }
        public long SinId { get; init; }
        public TimeSpan Duration { get; init; }
    }
}
