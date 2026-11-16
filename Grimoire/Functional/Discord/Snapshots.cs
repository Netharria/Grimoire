// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using LanguageExt;

namespace Grimoire.Functional.Discord;

// Copies of what Discord said at the time of a lookup (spec 1.4, section 3.2). DSharpPlus's entities can't be built in
// a test (plan C5), so IDiscordIO returns these instead, and pipelines that call Discord run against a fake. A
// snapshot isn't a domain entity and isn't stored. Each From is the one place its entity is read; a section that needs
// another field adds it there.

public sealed record ChannelSnapshot(
    ChannelId Id,
    Option<GuildId> Guild,
    string Name,
    DiscordChannelType Type,
    Option<ChannelId> Parent)
{
    [Pure]
    public static ChannelSnapshot From(DiscordChannel channel) =>
        new(channel.GetChannelId(),
            Optional(channel.GuildId).Map(id => new GuildId(id)),
            channel.Name ?? string.Empty,
            channel.Type,
            Optional(channel.ParentId).Map(id => new ChannelId(id)));
}

public sealed record UserSnapshot(
    UserId Id,
    Username Username,
    Option<string> GlobalName,
    string AvatarUrl,
    bool IsBot)
{
    [Pure]
    public static UserSnapshot From(DiscordUser user) =>
        new(user.GetUserId(),
            Username.Create(user.Username),
            Optional(user.GlobalName).Filter(name => name.Length > 0),
            user.AvatarUrl,
            user.IsBot);
}

/// <param name="AvatarUrl">The guild avatar if the member set one, else the user's (SH-4: one lookup, not two).</param>
/// <param name="TopRolePosition">The highest role's position, for the hierarchy checks of sections 3.3–3.5.</param>
public sealed record MemberSnapshot(
    GuildId Guild,
    UserSnapshot User,
    Option<string> Nickname,
    string AvatarUrl,
    Seq<RoleId> Roles,
    int TopRolePosition,
    DiscordPermissions Permissions,
    DateTimeOffset JoinedAt,
    Option<DateTimeOffset> TimedOutUntil)
{
    /// <summary>Server nickname, else global display name, else username: TD-9's display name.</summary>
    public string DisplayName =>
        this.Nickname.Filter(name => name.Length > 0)
            .IfNone(() => this.User.GlobalName.IfNone(this.User.Username.Value));

    [Pure]
    public static MemberSnapshot From(DiscordMember member) =>
        new(new GuildId(member.Guild.Id),
            UserSnapshot.From(member),
            Optional(member.Nickname),
            member.GuildAvatarUrl ?? member.AvatarUrl,
            toSeq(member.Roles.Select(role => new RoleId(role.Id))),
            member.Hierarchy,
            member.Permissions,
            member.JoinedAt,
            Optional(member.CommunicationDisabledUntil));
}

public sealed record RoleSnapshot(
    RoleId Id,
    GuildId Guild,
    string Name,
    int Position,
    DiscordPermissions Permissions,
    bool IsManaged)
{
    [Pure]
    public static RoleSnapshot From(GuildId guild, DiscordRole role) =>
        new(new RoleId(role.Id), guild, role.Name, role.Position, role.Permissions, role.IsManaged);
}

public sealed record MessageSnapshot(
    MessageId Id,
    ChannelId Channel,
    Option<GuildId> Guild,
    UserId Author,
    string Content,
    DateTimeOffset CreatedAt,
    Option<DateTimeOffset> EditedAt,
    Seq<string> AttachmentUrls)
{
    [Pure]
    public static MessageSnapshot From(DiscordMessage message) =>
        new(new MessageId(message.Id),
            new ChannelId(message.ChannelId),
            Optional(message.Channel?.GuildId).Map(id => new GuildId(id)),
            new UserId(message.Author?.Id ?? 0),
            message.Content,
            message.Timestamp,
            Optional(message.EditedTimestamp),
            toSeq(message.Attachments.Select(attachment => attachment.Url ?? string.Empty)
                .Where(url => url.Length > 0)));
}
