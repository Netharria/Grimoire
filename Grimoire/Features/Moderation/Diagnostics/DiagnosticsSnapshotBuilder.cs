// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Diagnostics;

public static class DiagnosticsSnapshotBuilder
{
    /// <summary>Copies what the checks need out of the (cached) guild. Makes no API calls.</summary>
    public static GuildSnapshot Build(DiscordGuild guild, RoleId? muteRoleId)
    {
        var bot = guild.CurrentMember;
        var muteRole = muteRoleId is { } id && guild.Roles.TryGetValue(id.Value, out var role) ? role : null;

        return new GuildSnapshot(
            guild.Id,
            [.. guild.Roles.Values.Select(r => new RoleSnapshot(r.Id, r.Name, r.Permissions))],
            muteRoleId?.Value,
            muteRole?.Position,
            bot.Permissions,
            bot.Hierarchy,
            [.. guild.Channels.Values.Select(channel => ToSnapshot(channel, bot)).OfType<ChannelSnapshot>()]);
    }

    // Announcement channels are moderator only and threads inherit from their parent, so neither is checked.
    private static ChannelSnapshot? ToSnapshot(DiscordChannel channel, DiscordMember bot)
        => (channel.Type switch
        {
            DiscordChannelType.Text or DiscordChannelType.GuildForum or DiscordChannelType.GuildMedia
                => (ChannelKind?)ChannelKind.Text,
            DiscordChannelType.Voice or DiscordChannelType.Stage => ChannelKind.Voice,
            _ => null
        }) is { } kind
            ? new ChannelSnapshot(
                channel.Id,
                channel.Name,
                kind,
                [
                    .. channel.PermissionOverwrites
                        .Where(o => o.Type is DiscordOverwriteType.Role or DiscordOverwriteType.Member)
                        .Select(o => new OverwriteSnapshot(
                            o.Id,
                            o.Type is DiscordOverwriteType.Role ? OverwriteTarget.Role : OverwriteTarget.Member,
                            o.Allowed,
                            o.Denied))
                ],
                channel.PermissionsFor(bot).HasPermission(DiscordPermission.ManageChannels))
            : null;
}
