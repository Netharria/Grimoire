// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Discord.Attributes;
using DSharpPlus.SlashCommands.Attributes;
using DSharpPlus.SlashCommands;
using DSharpPlus;
using Cybermancy.Core.Enums;
using Mediator;
using Cybermancy.Discord.Extensions;
using Cybermancy.Discord.Structs;
using Cybermancy.Domain;
using DSharpPlus.Entities;
using Cybermancy.Core.Features.Moderation.Queries.GetBan;
using Cybermancy.Core.Features.Moderation.Commands.BanComands.PublishBan;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;
using Cybermancy.Core.Exceptions;

namespace Cybermancy.Discord.ModerationModule
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequirePermissions(Permissions.ManageMessages)]
    [SlashCommandGroup("Publish", "Publishes a ban or unban to the public ban log channel.")]
    public class Publish_Commands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public Publish_Commands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("Ban", "Publishes a ban to the public ban log channel.")]
        public async Task PublishBanAsync(
            InteractionContext ctx,
            [Minimum(0)]
            [Option("Sin Id", "The id of the sin to be published")] long sinId)
        {
            var response = await this._mediator.Send(new GetBanQuery
            {
                SinId = sinId,
                GuildId = ctx.Guild.Id
            });

            var banLogMessage = await SendPublicLogMessage(ctx, response, PublishType.Ban);

            await this._mediator.Send(new PublishBanCommand
            {
                SinId = sinId,
                MessageId = banLogMessage.Id,
                PublishType = PublishType.Ban
            });

            await ctx.ReplyAsync(CybermancyColor.Green, message: $"Successfully published ban : {sinId}");

            if (response.LogChannelId is null) return;
            var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.LogChannelId.Value);

            if (logChannel is null) return;
            await logChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator} published ban reason of sin {sinId}")
                .WithColor(CybermancyColor.Purple));
        }

        [SlashCommand("Unban", "Publishes an unban to the public ban log channel.")]
        public async Task PublishUnbanAsync(
            InteractionContext ctx,
            [Minimum(0)]
            [Option("Sin Id", "The id of the sin to be published")] long sinId)
        {
            var response = await this._mediator.Send(new GetUnbanQuery
            {
                SinId = sinId,
                GuildId = ctx.Guild.Id
            });

            var banLogMessage = await SendPublicLogMessage(ctx, response, PublishType.Unban);

            await this._mediator.Send(new PublishBanCommand
            {
                SinId = sinId,
                MessageId = banLogMessage.Id,
                PublishType = PublishType.Unban
            });

            await ctx.ReplyAsync(CybermancyColor.Green, message: $"Successfully published unban : {sinId}");

            if (response.LogChannelId is null) return;
            var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.LogChannelId.Value);

            if (logChannel is null) return;
            await logChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator} published unban reason of sin {sinId}")
                .WithColor(CybermancyColor.Purple));
        }

        private static async Task<DiscordMessage> SendPublicLogMessage(InteractionContext ctx, GetBanQueryResponse response, PublishType publish)
        {
            var banLogChannel = ctx.Guild.Channels.GetValueOrDefault(response.BanLogId);

            if (banLogChannel is null)
                throw new AnticipatedException("Could not find the ban log channel.");

            if(response.PublishedMessage is not null)
            {
                try
                {
                    var message = await banLogChannel.GetMessageAsync(response.PublishedMessage.Value);
                    return await message.ModifyAsync(new DiscordEmbedBuilder()
                        .WithTitle(publish.ToString())
                        .WithDescription($"**Date:** {response.Date}\n" +
                                        $"**User:** {response.Username} ({response.UserId})\n" +
                                        $"**Reason:** {response.Reason}")
                        .WithColor(CybermancyColor.Purple).Build());
                } catch(NotFoundException ex)
                {
                    ctx.Client.Logger.LogWarning(ex, "Could not find published message {id}", response.PublishedMessage);
                }
            }
            
            return await banLogChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithTitle(publish.ToString())
                .WithDescription($"**Date:** {response.Date}\n" +
                                $"**User:** {response.Username} ({response.UserId})\n" +
                                $"**Reason:** {response.Reason}")
                .WithColor(CybermancyColor.Purple));
        }
    }
}
