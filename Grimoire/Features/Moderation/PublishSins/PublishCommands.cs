// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.PublishSins;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
[Command("Publish")]
[Description("Publishes a ban or unban to the public ban log channel.")]
public sealed partial class PublishCommands(IMediator mediator, ILogger<PublishCommands> logger)

{
    private readonly ILogger<PublishCommands> _logger = logger;
    private readonly IMediator _mediator = mediator;

    private static async Task<DiscordMessage> SendPublicLogMessage(SlashCommandContext ctx,
        GetBanForPublish.Response response,
        PublishType publish, ILogger<PublishCommands> logger)
    {
        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var banLogChannel = ctx.Guild.Channels.GetValueOrDefault(response.BanLogId);

        if (banLogChannel is null)
            throw new AnticipatedException("Could not find the ban log channel.");

        var username = response.Username;
        if (string.IsNullOrWhiteSpace(username))
        {
            var user = await ctx.Client.GetUserAsync(response.UserId);
            username = user.Username;
        }

        if (response.PublishedMessage is not null)
            try
            {
                var message = await banLogChannel.GetMessageAsync(response.PublishedMessage.Value);
                return await message.ModifyAsync(new DiscordEmbedBuilder()
                    .WithTitle(publish.ToString())
                    .WithDescription(
                        $"**Date:** {Formatter.Timestamp(response.Date, TimestampFormat.ShortDateTime)}\n" +
                        $"**User:** {username} ({response.UserId})\n" +
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
                             $"**User:** {username} ({response.UserId})\n" +
                             $"**Reason:** {response.Reason}")
            .WithColor(GrimoireColor.Purple));
    }

    [LoggerMessage(LogLevel.Warning, "Could not find published message {id}")]
    static partial void LogPublishedMessageNotFound(ILogger<PublishCommands> logger, Exception ex, ulong? id);
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
