// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Features.Moderation.Commands;
using Grimoire.Features.Moderation.Queries;
using Microsoft.Extensions.Logging;

namespace Grimoire.ModerationModule;

[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Moderation)]
[SlashRequireUserGuildPermissions(DiscordPermissions.ManageMessages)]
[SlashCommandGroup("Publish", "Publishes a ban or unban to the public ban log channel.")]
public sealed partial class PublishCommands(IMediator mediator, ILogger<PublishCommands> logger) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;
    private readonly ILogger<PublishCommands> _logger = logger;

    [SlashCommand("Ban", "Publishes a ban to the public ban log channel.")]
    public async Task PublishBanAsync(
        InteractionContext ctx,
        [Minimum(0)]
        [Option("SinId", "The id of the sin to be published")] long sinId)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(new GetBanQuery
        {
            SinId = sinId,
            GuildId = ctx.Guild.Id
        });

        var banLogMessage = await SendPublicLogMessage(ctx, response, PublishType.Ban, _logger);
        if (response.PublishedMessage is null)
        {
            await this._mediator.Send(new PublishBanCommand
            {
                SinId = sinId,
                MessageId = banLogMessage.Id,
                PublishType = PublishType.Ban
            });
        }

        await ctx.EditReplyAsync(GrimoireColor.Green, message: $"Successfully published ban : {sinId}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{ctx.Member.GetUsernameWithDiscriminator()} published ban reason of sin {sinId}");
    }

    [SlashCommand("Unban", "Publishes an unban to the public ban log channel.")]
    public async Task PublishUnbanAsync(
        InteractionContext ctx,
        [Minimum(0)]
        [Option("SinId", "The id of the sin to be published")] long sinId)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(new GetUnbanQuery
        {
            SinId = sinId,
            GuildId = ctx.Guild.Id
        });

        var banLogMessage = await SendPublicLogMessage(ctx, response, PublishType.Unban, _logger);
        if (response.PublishedMessage is null)
        {
            await this._mediator.Send(new PublishBanCommand
            {
                SinId = sinId,
                MessageId = banLogMessage.Id,
                PublishType = PublishType.Unban
            });
        }

        await ctx.EditReplyAsync(GrimoireColor.Green, message: $"Successfully published unban : {sinId}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{ctx.Member.GetUsernameWithDiscriminator()} published unban reason of sin {sinId}");
    }

    private static async Task<DiscordMessage> SendPublicLogMessage(InteractionContext ctx, GetBanQueryResponse response, PublishType publish, ILogger<PublishCommands> logger)
    {
        var banLogChannel = ctx.Guild.Channels.GetValueOrDefault(response.BanLogId);

        if (banLogChannel is null)
            throw new AnticipatedException("Could not find the ban log channel.");

        if (response.PublishedMessage is not null)
        {
            try
            {
                var message = await banLogChannel.GetMessageAsync(response.PublishedMessage.Value);
                return await message.ModifyAsync(new DiscordEmbedBuilder()
                    .WithTitle(publish.ToString())
                    .WithDescription($"**Date:** {Formatter.Timestamp(response.Date, TimestampFormat.ShortDateTime)}\n" +
                                    $"**User:** {response.Username} ({response.UserId})\n" +
                                    $"**Reason:** {response.Reason}")
                    .WithColor(GrimoireColor.Purple).Build());
            }
            catch (NotFoundException ex)
            {
                LogPublishedMessageNotFound(logger, ex, response.PublishedMessage);
            }
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
