// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.PublishSins;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
[SlashCommandGroup("Publish", "Publishes a ban or unban to the public ban log channel.")]
public sealed partial class PublishCommands(IMediator mediator, ILogger<PublishCommands> logger)
    : ApplicationCommandModule
{
    private readonly ILogger<PublishCommands> _logger = logger;
    private readonly IMediator _mediator = mediator;

    private static async Task<DiscordMessage> SendPublicLogMessage(InteractionContext ctx,
        GetBanForPublish.Response response,
        PublishType publish, ILogger<PublishCommands> logger)
    {
        var banLogChannel = ctx.Guild.Channels.GetValueOrDefault(response.BanLogId);

        if (banLogChannel is null)
            throw new AnticipatedException("Could not find the ban log channel.");


        if (response.PublishedMessage is not null)
            try
            {
                var message = await banLogChannel.GetMessageAsync(response.PublishedMessage.Value);
                return await message.ModifyAsync(new DiscordEmbedBuilder()
                    .WithTitle(publish.ToString())
                    .WithDescription(
                        $"**Date:** {Formatter.Timestamp(response.Date, TimestampFormat.ShortDateTime)}\n" +
                        $"**User:** {response.Username} ({response.UserId})\n" +
                        $"**Reason:** {response.Reason}")
                    .WithColor(GrimoireColor.Purple).Build());
            }
            catch (NotFoundException ex)
            {
                LogPublishedMessageNotFound(logger, ex, response.PublishedMessage);
            }

        return await banLogChannel.SendMessageAsync(new DiscordEmbedBuilder()
            .WithTitle(publish.ToString())
            .WithDescription($"**Date:** {Formatter.Timestamp(response.Date, TimestampFormat.ShortDateTime)}\n" +
                             $"**User:** {response.Username} ({response.UserId})\n" +
                             $"**Reason:** {response.Reason}")
            .WithColor(GrimoireColor.Purple));
    }

    [LoggerMessage(LogLevel.Warning, "Could not find published message {id}")]
    private static partial void LogPublishedMessageNotFound(ILogger<PublishCommands> logger, Exception ex, ulong? id);
}

public sealed class PublishBan
{
    public sealed record Command : IRequest
    {
        public long SinId { get; init; }
        public ulong GuildId { get; init; }
        public ulong MessageId { get; init; }
        public PublishType PublishType { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory) : IRequestHandler<Command>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task Handle(Command command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.PublishedMessages.AddAsync(
                new PublishedMessage
                {
                    MessageId = command.MessageId, SinId = command.SinId, PublishType = command.PublishType
                }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
