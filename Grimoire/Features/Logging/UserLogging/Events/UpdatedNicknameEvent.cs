// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Notifications;

namespace Grimoire.Features.Logging.UserLogging.Events;

public sealed class UpdatedNicknameEvent
{
    public sealed class EventHandler(IMediator mediator, GuildLog guildLog) : IEventHandler<GuildMemberUpdatedEventArgs>
    {
        private readonly IMediator _mediator = mediator;
        private readonly GuildLog _guildLog = guildLog;

        public async Task HandleEventAsync(DiscordClient sender, GuildMemberUpdatedEventArgs args)
        {
            var nicknameResponse = await this._mediator.Send(new Command
            {
                GuildId = args.Guild.Id, UserId = args.Member.Id, Nickname = args.NicknameAfter
            });
            if (nicknameResponse is null
                || string.Equals(nicknameResponse.BeforeNickname,
                    nicknameResponse.AfterNickname,
                    StringComparison.CurrentCultureIgnoreCase))
                return;

            await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
            {
                GuildId = args.Guild.Id,
                GuildLogType = GuildLogType.NicknameUpdated,
                Embed = new DiscordEmbedBuilder()
                    .WithAuthor("Nickname Updated")
                    .AddField("User", args.Member.Mention)
                    .AddField("Before",
                        string.IsNullOrWhiteSpace(nicknameResponse.BeforeNickname)
                            ? "`None`"
                            : nicknameResponse.BeforeNickname, true)
                    .AddField("After",
                        string.IsNullOrWhiteSpace(nicknameResponse.AfterNickname)
                            ? "`None`"
                            : nicknameResponse.AfterNickname, true)
                    .WithThumbnail(args.Member.GetGuildAvatarUrl(MediaFormat.Auto))
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .WithColor(GrimoireColor.Mint)

            });

            await this._mediator.Publish(new NicknameUpdatedNotification
            {
                UserId = args.Member.Id,
                GuildId = args.Guild.Id,
                BeforeNickname = nicknameResponse.BeforeNickname,
                AfterNickname = nicknameResponse.AfterNickname
            });
        }
    }


    public sealed record Command : IRequest<UpdateNicknameCommandResponse?>
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
        public string? Nickname { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command, UpdateNicknameCommandResponse?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<UpdateNicknameCommandResponse?> Handle(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var currentNickname = await dbContext.NicknameHistory
                .AsNoTracking()
                .WhereMemberHasId(command.UserId, command.GuildId)
                .Where(x => x.Guild.UserLogSettings.ModuleEnabled)
                .OrderByDescending(x => x.Timestamp)
                .Select(x => new { x.Nickname, x.Guild.UserLogSettings.NicknameChannelLogId })
                .FirstOrDefaultAsync(cancellationToken);
            if (currentNickname is null
                || string.Equals(currentNickname.Nickname, command.Nickname, StringComparison.CurrentCultureIgnoreCase))
                return null;

            await dbContext.NicknameHistory.AddAsync(
                new NicknameHistory { GuildId = command.GuildId, UserId = command.UserId, Nickname = command.Nickname },
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new UpdateNicknameCommandResponse
            {
                BeforeNickname = currentNickname.Nickname,
                AfterNickname = command.Nickname,
                NicknameChannelLogId = currentNickname.NicknameChannelLogId
            };
        }
    }

    public sealed record UpdateNicknameCommandResponse
    {
        public string? BeforeNickname { get; init; }
        public string? AfterNickname { get; init; }
        public ulong? NicknameChannelLogId { get; init; }
    }
}
