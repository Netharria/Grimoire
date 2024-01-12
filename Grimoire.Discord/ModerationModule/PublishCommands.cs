// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Moderation.Commands;
using Grimoire.Core.Features.Moderation.Queries;
using Grimoire.Domain;
using Microsoft.Extensions.Logging;

namespace Grimoire.Discord.ModerationModule;

[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Moderation)]
[SlashRequireUserGuildPermissions(Permissions.ManageMessages)]
[SlashCommandGroup("Publish", "Publishes a ban or unban to the public ban log channel.")]
public partial class PublishCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

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

        var banLogMessage = await SendPublicLogMessage(ctx, response, PublishType.Ban);
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

        var banLogMessage = await SendPublicLogMessage(ctx, response, PublishType.Unban);
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

    private static async Task<DiscordMessage> SendPublicLogMessage(InteractionContext ctx, GetBanQueryResponse response, PublishType publish)
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
                LogPublishedMessageNotFound(ctx.Client.Logger, ex, response.PublishedMessage);
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
    private static partial void LogPublishedMessageNotFound(ILogger<BaseDiscordClient> logger, Exception ex, ulong? id);
}
