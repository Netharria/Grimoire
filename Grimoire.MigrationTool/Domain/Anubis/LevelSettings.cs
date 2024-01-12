// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Grimoire.MigrationTool.Domain.Anubis;

[Table("level_settings")]
public sealed record LevelSettings
{
    [Key]
    [Column("guild_id")]
    public ulong GuildId { get; set; }
    [Column("text_time")]
    public int TextTime { get; set; }
    [Column("base")]
    public int Base { get; set; }
    [Column("modifier")]
    public int Modifier { get; set; }
    [Column("amount")]
    public int Amount { get; set; }
    [Column("user_channel")]
    public ulong UserChannel { get; set; }
    [Column("log_channel")]
    public ulong LevelLog { get; set; }
}
