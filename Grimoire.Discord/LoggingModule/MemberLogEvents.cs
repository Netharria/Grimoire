// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Grimoire.Core.Features.LogCleanup.Commands;
using Grimoire.Core.Features.UserLogging.Commands;
using Grimoire.Core.Features.UserLogging.Queries;
using Grimoire.Discord.Notifications;
using Grimoire.Domain;

namespace Grimoire.Discord.LoggingModule;

[DiscordGuildMemberAddedEventSubscriber]
[DiscordGuildMemberUpdatedEventSubscriber]
[DiscordGuildMemberRemovedEventSubscriber]
internal class MemberLogEvents(IMediator mediator, IInviteService inviteService, IDiscordImageEmbedService imageEmbedService) :
    IDiscordGuildMemberAddedEventSubscriber,
    IDiscordGuildMemberUpdatedEventSubscriber,
    IDiscordGuildMemberRemovedEventSubscriber
{
    private readonly IMediator _mediator = mediator;
    private readonly IInviteService _inviteService = inviteService;
    private readonly IDiscordImageEmbedService _imageEmbedService = imageEmbedService;

    public async Task DiscordOnGuildMemberAdded(DiscordClient sender, GuildMemberAddEventArgs args)
    {
        var settings = await this._mediator.Send(new GetUserLogSettingsQuery{ GuildId = args.Guild.Id });
        if (!settings.IsLoggingEnabled) return;
        if (settings.JoinChannelLog is null) return;
        var logChannel = args.Guild.Channels.GetValueOrDefault(settings.JoinChannelLog.Value);
        if (logChannel is null) return;
        var invites = await args.Guild.GetInvitesAsync();
        var inviteUsed = this._inviteService.CalculateInviteUsed(new GuildInviteDto
        {
            GuildId = args.Guild.Id,
            Invites = new ConcurrentDictionary<string, Invite>(
                invites.Select(x =>
                    new Invite
                    {
                        Code = x.Code,
                        Inviter = x.Inviter.GetUsernameWithDiscriminator(),
                        Url = x.ToString(),
                        Uses = x.Uses,
                        MaxUses = x.MaxUses
                    }).ToDictionary(x => x.Code))
        });
        var inviteUsedText = "";
        if (inviteUsed is not null)
            inviteUsedText = $"{inviteUsed.Url} ({inviteUsed.Uses} uses)\n**Created By:** {inviteUsed.Inviter}";
        else if (!string.IsNullOrWhiteSpace(args.Guild.VanityUrlCode))
            inviteUsedText = $"Vanity Invite";
        else
            inviteUsedText = $"Unknown Invite";

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("User Joined")
            .AddField("Name", args.Member.Mention, true)
            .AddField("Created", Formatter.Timestamp(args.Member.CreationTimestamp), true)
            .AddField("Invite Used", inviteUsedText)
            .WithColor(args.Member.CreationTimestamp > DateTimeOffset.UtcNow.AddDays(-7) ? GrimoireColor.Yellow : GrimoireColor.Green)
            .WithThumbnail(args.Member.GetGuildAvatarUrl(ImageFormat.Auto))
            .WithFooter($"Total Members: {args.Guild.MemberCount}")
            .WithTimestamp(DateTimeOffset.UtcNow);

        if (args.Member.CreationTimestamp > DateTimeOffset.UtcNow.AddDays(-7))
            embed.AddField("New Account", $"Created {Formatter.Timestamp(args.Member.CreationTimestamp)}");
        await logChannel.SendMessageAsync(embed);
    }

    public async Task DiscordOnGuildMemberRemoved(DiscordClient sender, GuildMemberRemoveEventArgs args)
    {
        var settings = await this._mediator.Send(new GetUserLogSettingsQuery{ GuildId = args.Guild.Id });
        if (!settings.IsLoggingEnabled) return;
        if (settings.LeaveChannelLog is null) return;
        var logChannel = args.Guild.Channels.GetValueOrDefault(settings.LeaveChannelLog.Value);
        if (logChannel is null) return;

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("User Left")
            .AddField("Name", args.Member.Mention, true)
            .AddField("Created", Formatter.Timestamp(args.Member.CreationTimestamp), true)
            .AddField("Joined", Formatter.Timestamp(args.Member.JoinedAt), true)
            .WithColor(GrimoireColor.Red)
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
        await this.ProcessNicknameChanges(args);
        await this.ProcessUsernameChanges(args);
        await this.ProcessAvatarChanges(args);
    }

    private async Task ProcessNicknameChanges(GuildMemberUpdateEventArgs args)
    {
        var nicknameResponse = await this._mediator.Send(new UpdateNicknameCommand
        {
            GuildId = args.Guild.Id,
            UserId = args.Member.Id,
            Nickname = args.NicknameAfter
        });
        if (nicknameResponse is not null && !string.Equals(nicknameResponse.BeforeNickname, nicknameResponse.AfterNickname, StringComparison.CurrentCultureIgnoreCase))
        {
            if (nicknameResponse.NicknameChannelLogId is not null
                && args.Guild.Channels.TryGetValue(nicknameResponse.NicknameChannelLogId.Value, out var logChannel))
            {
                var embed = new DiscordEmbedBuilder()
                .WithAuthor("Nickname Updated")
                .AddField("User", $"<@!{args.Member.Id}>")
                .AddField("Before", string.IsNullOrWhiteSpace(nicknameResponse.BeforeNickname)? "`None`" : nicknameResponse.BeforeNickname, true)
                .AddField("After", string.IsNullOrWhiteSpace(nicknameResponse.AfterNickname)? "`None`" : nicknameResponse.AfterNickname, true)
                .WithThumbnail(args.Member.GetGuildAvatarUrl(ImageFormat.Auto))
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithColor(GrimoireColor.Mint);
                var message = await logChannel.SendMessageAsync(embed);
                if (message is null) return;
                await this._mediator.Send(new AddLogMessageCommand { MessageId = message.Id, ChannelId = message.ChannelId, GuildId = args.Guild.Id });
            }
            await this._mediator.Publish(new NicknameTrackerNotification
            {
                UserId = args.Member.Id,
                GuildId = args.Guild.Id,
                Username = args.Member.GetUsernameWithDiscriminator(),
                BeforeNickname = nicknameResponse.BeforeNickname,
                AfterNickname = nicknameResponse.AfterNickname
            });
        }
    }

    private async Task ProcessUsernameChanges(GuildMemberUpdateEventArgs args)
    {
        var usernameResponse = await this._mediator.Send(new UpdateUsernameCommand
        {
            GuildId = args.Guild.Id,
            UserId = args.Member.Id,
            Username = args.MemberAfter.GetUsernameWithDiscriminator()
        });
        if (usernameResponse is not null && !string.Equals(usernameResponse.BeforeUsername, usernameResponse.AfterUsername, StringComparison.CurrentCultureIgnoreCase))
        {
            if (usernameResponse.UsernameChannelLogId is not null
                && args.Guild.Channels.TryGetValue(usernameResponse.UsernameChannelLogId.Value, out var logChannel))
            {
                var embed = new DiscordEmbedBuilder()
                        .WithAuthor("Username Updated")
                        .AddField("User", $"<@!{args.MemberAfter.Id}>")
                        .AddField("Before", string.IsNullOrWhiteSpace(usernameResponse.BeforeUsername)? "`Unknown`" : usernameResponse.BeforeUsername, true)
                        .AddField("After", string.IsNullOrWhiteSpace(usernameResponse.AfterUsername)? "`Unknown`" : usernameResponse.AfterUsername, true)
                        .WithThumbnail(args.MemberAfter.GetAvatarUrl(ImageFormat.Auto))
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .WithColor(GrimoireColor.Mint);
                var message = await logChannel.SendMessageAsync(embed);
                if (message is null) return;
                await this._mediator.Send(new AddLogMessageCommand { MessageId = message.Id, ChannelId = message.ChannelId, GuildId = args.Guild.Id });
            }
            await this._mediator.Publish(new UsernameTrackerNotification
            {
                UserId = args.Member.Id,
                GuildId = args.Guild.Id,
                BeforeUsername = usernameResponse.BeforeUsername,
                AfterUsername = usernameResponse.AfterUsername
            });
        }
    }

    private async Task ProcessAvatarChanges(GuildMemberUpdateEventArgs args)
    {
        var avatarResponse = await this._mediator.Send(new UpdateAvatarCommand
        {
            GuildId = args.Guild.Id,
            UserId = args.Member.Id,
            AvatarUrl = args.MemberAfter.GetGuildAvatarUrl(ImageFormat.Auto, 128)
        });
        if (avatarResponse is not null && !string.Equals(avatarResponse.BeforeAvatar, avatarResponse.AfterAvatar, StringComparison.Ordinal))
        {
            if (avatarResponse.AvatarChannelLogId is not null
                && args.Guild.Channels.TryGetValue(avatarResponse.AvatarChannelLogId.Value, out var logChannel))
            {
                var embed = new DiscordEmbedBuilder()
                .WithAuthor("Avatar Updated")
                .WithDescription($"**User:** <@!{args.Member.Id}>\n\n" +
                    $"Old avatar in thumbnail. New avatar down below")
                .WithThumbnail(avatarResponse.BeforeAvatar)
                .WithColor(GrimoireColor.Purple)
                .WithTimestamp(DateTimeOffset.UtcNow);
                var messageBuilder = await this._imageEmbedService
                    .BuildImageEmbedAsync([avatarResponse.AfterAvatar],
                    args.Member.Id,
                    embed,
                    false);
                var message = await logChannel.SendMessageAsync(messageBuilder);
                if (message is null) return;
                await this._mediator.Send(new AddLogMessageCommand { MessageId = message.Id, ChannelId = message.ChannelId, GuildId = args.Guild.Id });
            }
            await this._mediator.Publish(new AvatarTrackerNotification
            {
                UserId = args.Member.Id,
                GuildId = args.Guild.Id,
                Username = args.Member.GetUsernameWithDiscriminator(),
                BeforeAvatar = avatarResponse.BeforeAvatar,
                AfterAvatar = avatarResponse.AfterAvatar
            });
        }
    }
}
