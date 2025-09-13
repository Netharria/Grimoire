// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Notifications;

namespace Grimoire.Features.Logging.UserLogging.Events;

public sealed class UpdatedAvatarEvent
{
    public sealed class EventHandler(
        IMediator mediator,
        IDiscordImageEmbedService imageEmbedService,
        GuildLog guildLog)
        : IEventHandler<GuildMemberUpdatedEventArgs>
    {
        private readonly IDiscordImageEmbedService _imageEmbedService = imageEmbedService;
        private readonly GuildLog _guildLog = guildLog;

        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, GuildMemberUpdatedEventArgs args)
        {
            var avatarResponse = await this._mediator.Send(new Command
            {
                GuildId = args.Guild.Id,
                UserId = args.Member.Id,
                AvatarUrl = args.MemberAfter.GetGuildAvatarUrl(MediaFormat.Auto, 128)
            });
            if (avatarResponse is null
                || string.Equals(
                    avatarResponse.BeforeAvatar,
                    avatarResponse.AfterAvatar,
                    StringComparison.Ordinal))
                return;
            var embed = new DiscordEmbedBuilder()
                .WithAuthor("Avatar Updated")
                .WithDescription($"**User:** {args.Member.Mention}\n\n" +
                                 $"Old avatar in thumbnail. New avatar down below")
                .WithThumbnail(avatarResponse.BeforeAvatar)
                .WithColor(GrimoireColor.Purple)
                .WithTimestamp(DateTimeOffset.UtcNow);

            await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomMessage
            {
                GuildId = args.Guild.Id,
                GuildLogType = GuildLogType.AvatarUpdated,
                Message = await this._imageEmbedService
                    .BuildImageEmbedAsync(
                        [avatarResponse.AfterAvatar],
                        args.Member.Id,
                        embed,
                        false)
            });

            await this._mediator.Publish(new AvatarUpdatedNotification
            {
                UserId = args.Member.Id,
                GuildId = args.Guild.Id,
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
                .Select(x => x.FileName)
                .FirstOrDefaultAsync(cancellationToken);
            if (currentAvatar is null
                || string.Equals(currentAvatar, command.AvatarUrl, StringComparison.Ordinal))
                return null;

            await dbContext.Avatars.AddAsync(
                new Avatar { GuildId = command.GuildId, UserId = command.UserId, FileName = command.AvatarUrl },
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new Response
            {
                BeforeAvatar = currentAvatar,
                AfterAvatar = command.AvatarUrl,
            };
        }
    }

    public sealed record Response
    {
        public string BeforeAvatar { get; init; } = string.Empty;
        public string AfterAvatar { get; init; } = string.Empty;
    }
}
