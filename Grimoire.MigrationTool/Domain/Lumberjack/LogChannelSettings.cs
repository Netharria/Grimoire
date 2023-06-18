// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Grimoire.MigrationTool.Domain.Lumberjack;

[Table("log_channels")]
public record LogChannelSettings
{
    [Key]
    [Column("guildid")]
    public ulong GuildId { get; set; }
    [Column("joinid")]
    public ulong JoinLogId { get; set; }
    [Column("leaveid")]
    public ulong LeaveLogId { get; set; }
    [Column("deleteid")]
    public ulong DeleteLogId { get; set; }
    [Column("delete_bulk")]
    public ulong BulkDeleteLogId { get; set; }
    [Column("edit")]
    public ulong EditLogId { get; set; }
    [Column("username")]
    public ulong UsernameLogId { get; set; }
    [Column("nickname")]
    public ulong NicknameLogId { get; set; }
    [Column("avatar")]
    public ulong AvatarLogId { get; set; }
    [Column("stat_member")]
    public ulong MemberCountStatChannel { get; set; }
    [Column("ljid")]
    public ulong? ModLogId { get; set; }
}
