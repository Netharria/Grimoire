// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.LogCleanup.Commands;
using Grimoire.Notifications;

namespace Grimoire.Features.Logging.UserLogging.Events;

public sealed class UpdatedNicknameEvent
{

    public sealed class EventHandler(IMediator mediator) : IEventHandler<GuildMemberUpdatedEventArgs>
    {
        private readonly IMediator _mediator = mediator;
        public async Task HandleEventAsync(DiscordClient sender, GuildMemberUpdatedEventArgs args)
        {
            var nicknameResponse = await this._mediator.Send(new Command
            {
                GuildId = args.Guild.Id,
                UserId = args.Member.Id,
                Nickname = args.NicknameAfter
            });
            if (nicknameResponse is null
                || string.Equals(nicknameResponse.BeforeNickname,
                nicknameResponse.AfterNickname,
                StringComparison.CurrentCultureIgnoreCase))
                return;

            var message = await sender.SendMessageToLoggingChannel(nicknameResponse.NicknameChannelLogId,
                new DiscordEmbedBuilder()
                .WithAuthor("Nickname Updated")
                .AddField("User", args.Member.Mention)
                .AddField("Before", string.IsNullOrWhiteSpace(nicknameResponse.BeforeNickname) ? "`None`" : nicknameResponse.BeforeNickname, true)
                .AddField("After", string.IsNullOrWhiteSpace(nicknameResponse.AfterNickname) ? "`None`" : nicknameResponse.AfterNickname, true)
                .WithThumbnail(args.Member.GetGuildAvatarUrl(ImageFormat.Auto))
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithColor(GrimoireColor.Mint));

            if (message is null)
                return;

            await this._mediator.Send(new AddLogMessage.Command { MessageId = message.Id, ChannelId = message.ChannelId, GuildId = args.Guild.Id });

            await this._mediator.Publish(new NicknameUpdatedNotification
            {
                UserId = args.Member.Id,
                GuildId = args.Guild.Id,
                Username = args.Member.GetUsernameWithDiscriminator(),
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

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Command, UpdateNicknameCommandResponse?>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<UpdateNicknameCommandResponse?> Handle(Command command, CancellationToken cancellationToken)
        {
            var currentNickname = await this._grimoireDbContext.NicknameHistory
            .AsNoTracking()
            .WhereMemberHasId(command.UserId, command.GuildId)
            .Where(x => x.Guild.UserLogSettings.ModuleEnabled)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => new
            {
                x.Nickname,
                x.Guild.UserLogSettings.NicknameChannelLogId
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (currentNickname is null
                || string.Equals(currentNickname.Nickname, command.Nickname, StringComparison.CurrentCultureIgnoreCase))
                return null;

            await this._grimoireDbContext.NicknameHistory.AddAsync(
                new NicknameHistory
                {
                    GuildId = command.GuildId,
                    UserId = command.UserId,
                    Nickname = command.Nickname
                }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new UpdateNicknameCommandResponse
            {
                BeforeNickname = currentNickname.Nickname,
                AfterNickname = command.Nickname,
                NicknameChannelLogId = currentNickname.NicknameChannelLogId
            };
        }
    }

    public sealed record UpdateNicknameCommandResponse : BaseResponse
    {
        public string? BeforeNickname { get; init; }
        public string? AfterNickname { get; init; }
        public ulong? NicknameChannelLogId { get; init; }
    }
}
