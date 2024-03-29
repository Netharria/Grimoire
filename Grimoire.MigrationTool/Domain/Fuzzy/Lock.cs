// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Grimoire.MigrationTool.Domain.Fuzzy;

[Table("locks")]
internal sealed class Lock
{
    [Key]
    [Column("channel_id")]
    public ulong ChannelId { get; set; }
    [Column("previous_value")]
    public bool PreviousValue { get; set; }
    [Column("moderator_id")]
    public ulong ModeratorId { get; set; }
    [Column("moderator_name")]
    public string ModeratorName { get; set; } = string.Empty;
    [Column("guild_id")]
    public ulong GuildId { get; set; }
    [Column("reason")]
    public string? Reason { get; set; }
    [Column("end_time")]
    public DateTime EndTime { get; set; }
}
