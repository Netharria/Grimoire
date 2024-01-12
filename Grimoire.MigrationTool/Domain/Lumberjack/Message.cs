// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Grimoire.MigrationTool.Domain.Lumberjack;

[Table("messages")]
public sealed record Message
{
    [Key]
    [Column("id")]
    public ulong Id { get; set; }
    [Column("author")]
    public ulong AuthorId { get; set; }
    [Column("authorname")]
    public string AuthorName { get; set; } = string.Empty;
    [Column("authordisplayname")]
    public string AuthorDisplayName { get; set; } = string.Empty;
    [Column("channelid")]
    public ulong ChannelId { get; set; }
    [Column("channelname")]
    public string ChannelName { get; set; } = string.Empty;
    [Column("guildid")]
    public ulong GuildId { get; set; }
    [Column("clean_content")]
    public string CleanContent { get; set; } = string.Empty;
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    [Column("pfp")]
    public string AvatarUrl { get; set; } = string.Empty;
    [Column("attachments")]
    public bool HasAttachments { get; set; }
}
