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

public sealed class UpdatedUsernameEvent
{
    public sealed class EventHandler(IMediator mediator) : IEventHandler<GuildMemberUpdatedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, GuildMemberUpdatedEventArgs args)
        {
            var usernameResponse = await this._mediator.Send(new Command
            {
                GuildId = args.Guild.Id,
                UserId = args.Member.Id,
                Username = args.MemberAfter.GetUsernameWithDiscriminator()
            });
            if (usernameResponse is null
                || string.Equals(usernameResponse.BeforeUsername,
                    usernameResponse.AfterUsername,
                    StringComparison.CurrentCultureIgnoreCase))
                return;

            var message = await sender.SendMessageToLoggingChannel(usernameResponse.UsernameChannelLogId,
                embed => embed
                    .WithAuthor("Username Updated")
                    .AddField("User", args.MemberAfter.Mention)
                    .AddField("Before",
                        string.IsNullOrWhiteSpace(usernameResponse.BeforeUsername)
                            ? "`Unknown`"
                            : usernameResponse.BeforeUsername, true)
                    .AddField("After",
                        string.IsNullOrWhiteSpace(usernameResponse.AfterUsername)
                            ? "`Unknown`"
                            : usernameResponse.AfterUsername, true)
                    .WithThumbnail(args.MemberAfter.GetAvatarUrl(ImageFormat.Auto))
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .WithColor(GrimoireColor.Mint));

            if (message is null)
                return;
            await this._mediator.Send(new AddLogMessage.Command
            {
                MessageId = message.Id, ChannelId = message.ChannelId, GuildId = args.Guild.Id
            });

            await this._mediator.Publish(new UsernameTrackerNotification
            {
                UserId = args.Member.Id,
                GuildId = args.Guild.Id,
                BeforeUsername = usernameResponse.BeforeUsername,
                AfterUsername = usernameResponse.AfterUsername
            });
        }
    }

    public sealed record Command : IRequest<Response?>
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
        public string Username { get; init; } = string.Empty;
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var currentUsername = await dbContext.Members
                .AsNoTracking()
                .WhereMemberHasId(command.UserId, command.GuildId)
                .Where(member => member.Guild.UserLogSettings.ModuleEnabled)
                .Select(member => new
                {
                    member.User.UsernameHistories.OrderByDescending(x => x.Timestamp)
                        .First(usernameHistory => usernameHistory.Timestamp < DateTime.UtcNow.AddSeconds(-2)).Username,
                    member.Guild.UserLogSettings.UsernameChannelLogId
                }).FirstOrDefaultAsync(cancellationToken);
            if (currentUsername is null
                || string.Equals(currentUsername.Username, command.Username, StringComparison.CurrentCultureIgnoreCase))
                return null;

            await dbContext.UsernameHistory.AddAsync(
                new UsernameHistory { UserId = command.UserId, Username = command.Username }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new Response
            {
                BeforeUsername = currentUsername.Username,
                AfterUsername = command.Username,
                UsernameChannelLogId = currentUsername.UsernameChannelLogId
            };
        }
    }

    public sealed record Response : BaseResponse
    {
        public string BeforeUsername { get; init; } = string.Empty;
        public string AfterUsername { get; init; } = string.Empty;
        public ulong? UsernameChannelLogId { get; init; }
    }
}
