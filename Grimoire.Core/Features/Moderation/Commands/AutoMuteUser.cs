// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Moderation.Commands;

public sealed class AutoMuteUser {

    public sealed record Command : ICommand<Response>
    {
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
        public required ulong ModeratorId { get; init; }
        public required string Reason { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : ICommandHandler<Command, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response> Handle(Command command, CancellationToken cancellationToken)
        {
            var response = await this._grimoireDbContext.Members
            .WhereMemberHasId(command.UserId, command.GuildId)
            .Select(x => new
            {
                x.ActiveMute,
                x.Guild.ModerationSettings.MuteRole,
                x.Guild.ModChannelLog,
                MuteCount = x.UserSins.Where(x => x.SinType == SinType.Mute)
                    .Where(x => x.SinOn > DateTimeOffset.UtcNow.AddDays(-1))
                    .Count()
            }).FirstOrDefaultAsync(cancellationToken);
            if (response is null) throw new AnticipatedException("Could not find User.");
            if (response.MuteRole is null) throw new AnticipatedException("A mute role is not configured.");
            if (response.ActiveMute is not null) this._grimoireDbContext.Mutes.Remove(response.ActiveMute);
            var duration = TimeSpan.FromMinutes(Math.Pow(2, response.MuteCount));
            var sin = new Sin
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                ModeratorId = command.ModeratorId,
                Reason = command.Reason,
                SinType = SinType.Mute,
                Mute = new Mute
                {
                    GuildId = command.GuildId,
                    UserId = command.UserId,
                    EndTime = DateTimeOffset.UtcNow + duration,
                }
            };
            await this._grimoireDbContext.Sins.AddAsync(sin, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new Response
            {
                MuteRole = response.MuteRole.Value,
                LogChannelId = response.ModChannelLog,
                SinId = sin.Id,
                Duration = duration,
            };
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong MuteRole { get; init; }
        public long SinId { get; init; }
        public TimeSpan Duration { get; init; }
    }
}




