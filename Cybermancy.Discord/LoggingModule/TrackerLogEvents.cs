// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Logging.Queries.GetAllTrackersForUser;
using Cybermancy.Core.Features.Logging.Queries.GetTracker;
using Cybermancy.Core.Features.Logging.Queries.GetTrackerWithOldMessage;
using Cybermancy.Discord.Extensions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using MediatR;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.Discord.LoggingModule
{
    [DiscordMessageCreatedEventSubscriber]
    [DiscordMessageUpdatedEventSubscriber]
    [DiscordVoiceStateUpdatedEventSubscriber]
    [DiscordGuildMemberUpdatedEventSubscriber]
    [DiscordUserUpdatedEventSubscriber]
    public class TrackerLogEvents :
        IDiscordMessageCreatedEventSubscriber,
        IDiscordMessageUpdatedEventSubscriber,
        IDiscordVoiceStateUpdatedEventSubscriber,
        IDiscordGuildMemberUpdatedEventSubscriber,
        IDiscordUserUpdatedEventSubscriber
    {
        private readonly IMediator _mediator;
        private readonly HttpClient _httpClient;

        public TrackerLogEvents(IMediator mediator, IHttpClientFactory httpFactory)
        {
            this._mediator = mediator;
            this._httpClient = httpFactory.CreateClient();
        }

        
        public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
        {
            var response = await _mediator.Send(new GetTrackerQuery{ UserId = args.Author.Id, GuildId = args.Guild.Id });
            if (response is null) return;

            var loggingChannel = args.Guild.Channels.GetValueOrDefault(response.TrackerChannelId);
            if (loggingChannel is null) return;

            var embeds = new List<DiscordEmbed>();

            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"{args.Message.Channel.Mention}")
                .WithDescription($"**[Jump URL]({args.Message.JumpLink})**")
                .WithFooter($"{args.Author.GetUsernameWithDiscriminator()}\t{args.Author.Id}\nMessage Sent",
                    args.Author.GetAvatarUrl(ImageFormat.Auto))
                .WithTimestamp(DateTime.UtcNow)
                .AddMessageTextToFields("**Content**", args.Message.Content, false);

            var files = new Dictionary<string, Stream>();

            if (args.Message.Attachments.Any())
            {
                foreach ((var attachment, var index) in args.Message.Attachments.Select((x, i) => (x, i)))
                {
                    if (string.IsNullOrWhiteSpace(attachment.FileName))
                        continue;

                    var url = new Uri(Path.Combine("https://cdn.discordapp.com/attachments/", args.Channel.Id.ToString(), attachment.Id.ToString(), attachment.FileName));
                    var stream = await this._httpClient.GetStreamAsync(url);
                    var fileName = $"attachment{index}.{attachment.FileName.Split('.')[^1]}";

                    var stride = 4 * (index / 4);
                    var attachments = args.Message.Attachments
                        .Skip(stride)
                        .Take(4)
                        .Select(x => $"**{x.FileName}**")
                        .ToArray();

                    var embedTest = new DiscordEmbedBuilder()
                        .WithColor(DiscordColor.Red)
                        .WithTitle(default)
                        .WithAuthor("Attachment(s) Deleted")
                        .WithUrl($"https://discord.com/users/{args.Author.Id}/{stride}")
                        .WithImageUrl($"attachment://{fileName}")
                        .WithDescription(string.Join(" | ", attachments));

                    embeds.Add(embedTest);
                    files.Add(fileName, stream);
                }
            }
            await loggingChannel.SendMessageAsync(new DiscordMessageBuilder()
            .AddEmbeds(embeds.Prepend(embed))
            .WithFiles(files));
        }
        public async Task DiscordOnMessageUpdated(DiscordClient sender, MessageUpdateEventArgs args)
        {
            if (args.Message.Content.Length == 0) return;
            var response = await this._mediator.Send(
                new GetTrackerWithOldMessageQuery
                {
                    UserId = args.Author.Id,
                    GuildId = args.Guild.Id,
                    MessageId = args.Message.Id
                });
            if (response is null) return;
            var loggingChannel = args.Guild.Channels.GetValueOrDefault(response.TrackerChannelId);
            if (loggingChannel is null) return;

            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"{args.Message.Channel.Mention}")
                .WithDescription($"**[Jump URL]({args.Message.JumpLink})**")
                .WithTimestamp(DateTime.UtcNow)
                .WithFooter($"{args.Author.GetUsernameWithDiscriminator()}\t{args.Author.Id}\nMessage Edited",
                    args.Author.GetAvatarUrl(ImageFormat.Auto))
                .AddMessageTextToFields("Before", response.OldMessageContent)
                .AddMessageTextToFields("After", args.Message.Content);

            await loggingChannel.SendMessageAsync(embed);
        }
        public async Task DiscordOnVoiceStateUpdated(DiscordClient sender, VoiceStateUpdateEventArgs args)
        {
            var response = await _mediator.Send(new GetTrackerQuery{ UserId = args.User.Id, GuildId = args.Guild.Id });
            if (response is null) return;

            var loggingChannel = args.Guild.Channels.GetValueOrDefault(response.TrackerChannelId);
            if (loggingChannel is null) return;

            if (args.Before.Channel == args.After.Channel) return;
            DiscordEmbedBuilder embed;
            if (args.Before.Channel is null)
            {
                embed = new DiscordEmbedBuilder()
                    .WithAuthor("Joined Voice Channel")
                    .WithDescription($"**Channel:** {args.After.Channel.Name}")
                    .WithFooter($"{args.User.GetUsernameWithDiscriminator()}\n{args.User.Id}")
                    .WithTimestamp(DateTime.UtcNow);
                await loggingChannel.SendMessageAsync(embed);
                return;
            }

            if (args.After.Channel is null)
            {
                embed = new DiscordEmbedBuilder()
                    .WithAuthor("Left Voice Channel")
                    .WithDescription($"**Channel:** {args.Before.Channel.Name}")
                    .WithFooter($"{args.User.GetUsernameWithDiscriminator()}\n{args.User.Id}")
                    .WithTimestamp(DateTime.UtcNow);
                await loggingChannel.SendMessageAsync(embed);
                return;
            }

            embed = new DiscordEmbedBuilder()
                    .WithAuthor("Moved Voice Channels")
                    .WithDescription($"**From:** {args.Before.Channel.Name}\n" +
                    $"**To:** {args.After.Channel.Name}")
                    .WithFooter($"{args.User.GetUsernameWithDiscriminator()}\n{args.User.Id}")
                    .WithTimestamp(DateTime.UtcNow);
            await loggingChannel.SendMessageAsync(embed);
        }
        public async Task DiscordOnGuildMemberUpdated(DiscordClient sender, GuildMemberUpdateEventArgs args)
        {
            var response = await _mediator.Send(new GetTrackerQuery{ UserId = args.Member.Id, GuildId = args.Guild.Id });
            if (response is null) return;
            if (args.NicknameBefore != args.NicknameAfter)
            {
                var logChannel = args.Guild.Channels.GetValueOrDefault(response.TrackerChannelId);
                if(logChannel is not null)
                {
                    var embed = new DiscordEmbedBuilder()
                    .WithDescription($"**Before:** {args.NicknameBefore}\n" +
                        $"**After:** {args.NicknameAfter}")
                    .WithAuthor(args.Member.GetUsernameWithDiscriminator())
                    .WithFooter($"{args.Member.Id}")
                    .WithTimestamp(DateTimeOffset.UtcNow);
                    await logChannel.SendMessageAsync(embed);
                }
            }
            if (args.AvatarHashBefore != args.AvatarHashAfter)
            {
                var logChannel = args.Guild.Channels.GetValueOrDefault(response.TrackerChannelId);
                if (logChannel is not null)
                {
                    var url = args.Member.GetAvatarUrl(ImageFormat.Auto);
                    var stream = await this._httpClient.GetStreamAsync(url);
                    var fileName = $"attachment{0}.{args.Member.GetAvatarUrl(ImageFormat.Auto).Split('.')[^1]}";

                    var embed = new DiscordEmbedBuilder()
                    .WithDescription($"New avatar")
                    .WithAuthor(args.Member.GetUsernameWithDiscriminator())
                    .WithThumbnail(args.Member.GetGuildAvatarUrl(ImageFormat.Auto))
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .WithImageUrl($"attachment://{fileName}");

                    await logChannel.SendMessageAsync(new DiscordMessageBuilder()
                        .AddEmbed(embed)
                        .WithFile(fileName, stream));
                }
            }
        }

        public async Task DiscordOnUserUpdated(DiscordClient sender, UserUpdateEventArgs args)
        {
            var response = await _mediator.Send(new GetAllTrackersForUserQuery{ UserId = args.UserAfter.Id });
            foreach(var tracker in response.Trackers)
            {
                var guild = sender.Guilds.GetValueOrDefault(tracker.GuildId);
                if (guild is null) continue;
                if (args.UserBefore.Username != args.UserAfter.Username)
                {
                    var logChannel = guild.Channels.GetValueOrDefault(tracker.TrackerChannelId);
                    if (logChannel is not null)
                    {
                        var embed = new DiscordEmbedBuilder()
                            .WithDescription($"**Before:** {args.UserBefore.Username}\n" +
                                $"**After:** {args.UserAfter.Username}")
                            .WithAuthor(args.UserAfter.GetUsernameWithDiscriminator())
                            .WithFooter($"{args.UserAfter.Id}")
                            .WithTimestamp(DateTimeOffset.UtcNow);
                        await logChannel.SendMessageAsync(embed);
                    }
                }
                if (args.UserBefore.AvatarHash != args.UserAfter.AvatarHash)
                {
                    var logChannel = guild.Channels.GetValueOrDefault(tracker.TrackerChannelId);
                    if (logChannel is not null)
                    {
                        var url = args.UserAfter.GetAvatarUrl(ImageFormat.Auto);
                        var stream = await this._httpClient.GetStreamAsync(url);
                        var fileName = $"attachment{0}.{args.UserAfter.GetAvatarUrl(ImageFormat.Auto).Split('.')[^1]}";

                        var embed = new DiscordEmbedBuilder()
                            .WithDescription($"New avatar")
                            .WithAuthor(args.UserAfter.GetUsernameWithDiscriminator())
                            .WithThumbnail(args.UserBefore.GetAvatarUrl(ImageFormat.Auto))
                            .WithTimestamp(DateTimeOffset.UtcNow)
                            .WithImageUrl($"attachment://{fileName}");

                        await logChannel.SendMessageAsync(new DiscordMessageBuilder()
                            .AddEmbed(embed)
                            .WithFile(fileName, stream));
                    }
                }
            }
            
        }
    }
}
