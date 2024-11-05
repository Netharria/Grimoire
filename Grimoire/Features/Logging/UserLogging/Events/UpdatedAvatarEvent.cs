// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.LogCleanup.Commands;
using Grimoire.Notifications;

namespace Grimoire.Features.Logging.UserLogging.Events;

public sealed class UpdatedAvatarEvent
{
    public sealed class EventHandler(IMediator mediator, IDiscordImageEmbedService imageEmbedService)
        : IEventHandler<GuildMemberUpdatedEventArgs>
    {
        private readonly IDiscordImageEmbedService _imageEmbedService = imageEmbedService;

        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, GuildMemberUpdatedEventArgs args)
        {
            var avatarResponse = await this._mediator.Send(new Command
            {
                GuildId = args.Guild.Id,
                UserId = args.Member.Id,
                AvatarUrl = args.MemberAfter.GetGuildAvatarUrl(ImageFormat.Auto, 128)
            });
            if (avatarResponse is null
                || string.Equals(
                    avatarResponse.BeforeAvatar,
                    avatarResponse.AfterAvatar,
                    StringComparison.Ordinal))
                return;


            var message = await sender.SendMessageToLoggingChannel(avatarResponse.AvatarChannelLogId,
                () =>
                {
                    var embed = new DiscordEmbedBuilder()
                        .WithAuthor("Avatar Updated")
                        .WithDescription($"**User:** {args.Member.Mention}\n\n" +
                                         $"Old avatar in thumbnail. New avatar down below")
                        .WithThumbnail(avatarResponse.BeforeAvatar)
                        .WithColor(GrimoireColor.Purple)
                        .WithTimestamp(DateTimeOffset.UtcNow);
                    return this._imageEmbedService
                        .BuildImageEmbedAsync([avatarResponse.AfterAvatar],
                            args.Member.Id,
                            embed,
                            false);
                });

            if (message is null)
                return;

            await this._mediator.Send(new AddLogMessage.Command
            {
                MessageId = message.Id, ChannelId = message.ChannelId, GuildId = args.Guild.Id
            });

            await this._mediator.Publish(new AvatarUpdatedNotification
            {
                UserId = args.Member.Id,
                GuildId = args.Guild.Id,
                Username = args.Member.GetUsernameWithDiscriminator(),
                BeforeAvatar = avatarResponse.BeforeAvatar,
                AfterAvatar = avatarResponse.AfterAvatar
            });
        }
    }

    public sealed record Command : IRequest<Response?>
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
        public string AvatarUrl { get; init; } = string.Empty;
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var currentAvatar = await dbContext.Avatars
                .AsNoTracking()
                .Where(x => x.UserId == command.UserId && x.GuildId == command.GuildId)
                .Where(x => x.Member.Guild.UserLogSettings.ModuleEnabled)
                .OrderByDescending(x => x.Timestamp)
                .Select(x => new { x.FileName, x.Member.Guild.UserLogSettings.AvatarChannelLogId })
                .FirstOrDefaultAsync(cancellationToken);
            if (currentAvatar is null
                || string.Equals(currentAvatar.FileName, command.AvatarUrl, StringComparison.Ordinal))
                return null;

            await dbContext.Avatars.AddAsync(
                new Avatar { GuildId = command.GuildId, UserId = command.UserId, FileName = command.AvatarUrl },
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new Response
            {
                BeforeAvatar = currentAvatar.FileName,
                AfterAvatar = command.AvatarUrl,
                AvatarChannelLogId = currentAvatar.AvatarChannelLogId
            };
        }
    }

    public sealed record Response : BaseResponse
    {
        public string BeforeAvatar { get; init; } = string.Empty;
        public string AfterAvatar { get; init; } = string.Empty;
        public ulong? AvatarChannelLogId { get; init; }
    }
}
