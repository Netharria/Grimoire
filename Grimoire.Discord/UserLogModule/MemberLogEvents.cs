// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Logging.Queries.GetUserLogSettings;

namespace Grimoire.Discord.LoggingModule
{
    [DiscordGuildMemberAddedEventSubscriber]
    [DiscordGuildMemberUpdatedEventSubscriber]
    [DiscordGuildMemberRemovedEventSubscriber]
    [DiscordUserUpdatedEventSubscriber]
    internal class MemberLogEvents :
        IDiscordGuildMemberAddedEventSubscriber,
        IDiscordGuildMemberUpdatedEventSubscriber,
        IDiscordGuildMemberRemovedEventSubscriber,
        IDiscordUserUpdatedEventSubscriber
    {
        private readonly IMediator _mediator;
        private readonly IInviteService _inviteService;
        private readonly HttpClient _httpClient;

        public MemberLogEvents(IMediator mediator, IInviteService inviteService, IHttpClientFactory httpFactory)
        {
            this._mediator = mediator;
            this._inviteService = inviteService;
            this._httpClient = httpFactory.CreateClient();
        }

        public async Task DiscordOnGuildMemberAdded(DiscordClient sender, GuildMemberAddEventArgs args)
        {
            var settings = await this._mediator.Send(new GetUserLogSettingsQuery{ GuildId = args.Guild.Id });
            if (!settings.IsLoggingEnabled) return;
            if (settings.JoinChannelLog is null) return;
            var logChannel = args.Guild.Channels.GetValueOrDefault(settings.JoinChannelLog.Value);
            if (logChannel is null) return;

            var accountAge = DateTime.UtcNow - args.Member.CreationTimestamp;
            var invites = await args.Guild.GetInvitesAsync();
            var inviteUsed = this._inviteService.CalculateInviteUsed(
                invites.Select(x =>
                new Domain.Invite{
                    Code = x.Code,
                    Inviter = x.Inviter.GetUsernameWithDiscriminator(),
                    Url = x.ToString(),
                    Uses = x.Uses }).ToList());

            var embed = new DiscordEmbedBuilder()
                .WithTitle("User Joined")
                .WithDescription($"**Name:** {args.Member.Mention}\n" +
                    $"**Created on:** {args.Member.CreationTimestamp:MMM dd, yyyy}\n" +
                    $"**Account age:** {accountAge.Days} days old\n" +
                    $"**Invite used:** {inviteUsed.Url} ({inviteUsed.Uses} uses)\n" +
                    $"**Created By:** {inviteUsed.Inviter}")
                .WithColor(accountAge < TimeSpan.FromDays(7) ? GrimoireColor.Orange : GrimoireColor.Green)
                .WithAuthor($"{args.Member.GetUsernameWithDiscriminator()} ({args.Member.Id})")
                .WithThumbnail(args.Member.GetGuildAvatarUrl(ImageFormat.Auto))
                .WithFooter($"Total Members: {args.Guild.MemberCount}")
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (accountAge < TimeSpan.FromDays(7))
                embed.AddField("New Account", $"Created {accountAge.CustomTimeSpanString()}");
            await logChannel.SendMessageAsync(embed);
        }
        public async Task DiscordOnGuildMemberRemoved(DiscordClient sender, GuildMemberRemoveEventArgs args)
        {
            var settings = await this._mediator.Send(new GetUserLogSettingsQuery{ GuildId = args.Guild.Id });
            if (!settings.IsLoggingEnabled) return;
            if (settings.LeaveChannelLog is null) return;
            var logChannel = args.Guild.Channels.GetValueOrDefault(settings.LeaveChannelLog.Value);
            if (logChannel is null) return;

            var accountAge = DateTimeOffset.UtcNow - args.Member.CreationTimestamp;
            var timeOnServer = DateTimeOffset.UtcNow - args.Member.JoinedAt;

            var embed = new DiscordEmbedBuilder()
                .WithTitle("User Left")
                .WithDescription($"**Name:** {args.Member.Mention}\n" +
                    $"**Created on:** {args.Member.CreationTimestamp:MMM dd, yyyy}\n" +
                    $"**Account age:** {accountAge.Days} days old\n" +
                    $"**Joined on:** {args.Member.JoinedAt:MMM dd, yyyy} ({timeOnServer.Days} days ago)")
                .WithColor(GrimoireColor.Purple)
                .WithAuthor($"{args.Member.GetUsernameWithDiscriminator()} ({args.Member.Id})")
                .WithThumbnail(args.Member.GetGuildAvatarUrl(ImageFormat.Auto))
                .WithFooter($"Total Members: {args.Guild.MemberCount}")
                .WithTimestamp(DateTimeOffset.UtcNow)
                .AddField($"Roles[{args.Member.Roles.Count()}]",
                args.Member.Roles.Any()
                ? string.Join(' ', args.Member.Roles.Where(x => x.Id != args.Guild.Id).Select(x => x.Mention))
                : "None");
            await logChannel.SendMessageAsync(embed);
        }
        public async Task DiscordOnGuildMemberUpdated(DiscordClient sender, GuildMemberUpdateEventArgs args)
        {
            var settings = await this._mediator.Send(new GetUserLogSettingsQuery{ GuildId = args.Guild.Id });
            if (!settings.IsLoggingEnabled) return;
            if (args.NicknameBefore != args.NicknameAfter && settings.NicknameChannelLog is not null)
            {
                var logChannel = args.Guild.Channels.GetValueOrDefault(settings.NicknameChannelLog.Value);
                if (logChannel is not null)
                {
                    var embed = new DiscordEmbedBuilder()
                    .WithTitle("Nickname Updated")
                    .WithDescription($"**User:** <@!{args.Member.Id}>\n\n" +
                        $"**Before:** {args.NicknameBefore}\n" +
                        $"**After:** {args.NicknameAfter}")
                    .WithAuthor($"{args.Member.GetUsernameWithDiscriminator()} ({args.Member.Id})")
                    .WithThumbnail(args.Member.GetGuildAvatarUrl(ImageFormat.Auto))
                    .WithTimestamp(DateTimeOffset.UtcNow);
                    await logChannel.SendMessageAsync(embed);
                }
            }
            if (args.AvatarHashBefore != args.AvatarHashAfter && settings.AvatarChannelLog is not null)
            {
                var logChannel = args.Guild.Channels.GetValueOrDefault(settings.AvatarChannelLog.Value);
                if (logChannel is not null)
                {
                    var url = args.Member.GetAvatarUrl(ImageFormat.Auto);
                    var stream = await this._httpClient.GetStreamAsync(url);
                    var fileName = $"attachment{0}.{args.Member.GetAvatarUrl(ImageFormat.Auto).Split('.')[^1]}";

                    var embed = new DiscordEmbedBuilder()
                    .WithTitle("Avatar Updated")
                    .WithDescription($"**User:** <@!{args.Member.Id}>\n\n" +
                        $"Old avatar in thumbnail. New avatar down below")
                    .WithAuthor($"{args.Member.GetUsernameWithDiscriminator()} ({args.Member.Id})")
                    .WithThumbnail(args.Member.GetGuildAvatarUrl(ImageFormat.Auto))
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .WithImageUrl($"attachment://{fileName}");

                    await logChannel.SendMessageAsync(new DiscordMessageBuilder()
                        .AddEmbed(embed)
                        .AddFile(fileName, stream));
                }
            }
        }
        public async Task DiscordOnUserUpdated(DiscordClient sender, UserUpdateEventArgs args)
        {
            foreach(var guild in sender.Guilds.Values)
            {
                if (!guild.Members.ContainsKey(args.UserAfter.Id)) continue;
                var settings = await this._mediator.Send(new GetUserLogSettingsQuery{ GuildId = guild.Id });
                if (!settings.IsLoggingEnabled) return;
                if (args.UserBefore.Username != args.UserAfter.Username && settings.UsernameChannelLog is not null)
                {
                    var logChannel = guild.Channels.GetValueOrDefault(settings.UsernameChannelLog.Value);
                    if (logChannel is not null)
                    {
                        var embed = new DiscordEmbedBuilder()
                            .WithTitle("Username Updated")
                            .WithDescription($"**User:** <@!{args.UserAfter.Id}>\n\n" +
                                $"**Before:** {args.UserBefore.Username}\n" +
                                $"**After:** {args.UserAfter.Username}")
                            .WithAuthor($"{args.UserAfter.GetUsernameWithDiscriminator()} ({args.UserAfter.Id})")
                            .WithThumbnail(args.UserAfter.GetAvatarUrl(ImageFormat.Auto))
                            .WithTimestamp(DateTimeOffset.UtcNow);
                        await logChannel.SendMessageAsync(embed);
                    }
                }
                if (args.UserBefore.AvatarHash != args.UserAfter.AvatarHash && settings.AvatarChannelLog is not null)
                {
                    var logChannel = guild.Channels.GetValueOrDefault(settings.AvatarChannelLog.Value);
                    if (logChannel is not null)
                    {
                        var url = args.UserAfter.GetAvatarUrl(ImageFormat.Auto);
                        var stream = await this._httpClient.GetStreamAsync(url);
                        var fileName = $"attachment{0}.{args.UserAfter.GetAvatarUrl(ImageFormat.Auto).Split('.')[^1]}";

                        var embed = new DiscordEmbedBuilder()
                            .WithTitle("Avatar Updated")
                            .WithDescription($"**User:** <@!{args.UserAfter.Id}>\n\n" +
                                $"Old avatar in thumbnail. New avatar down below")
                            .WithAuthor($"{args.UserAfter.GetUsernameWithDiscriminator()} ({args.UserAfter.Id})")
                            .WithThumbnail(args.UserBefore.GetAvatarUrl(ImageFormat.Auto))
                            .WithTimestamp(DateTimeOffset.UtcNow)
                            .WithImageUrl($"attachment://{fileName}");

                        await logChannel.SendMessageAsync(new DiscordMessageBuilder()
                            .AddEmbed(embed)
                            .AddFile(fileName, stream));
                    }
                }
            }
        }
    }
}
