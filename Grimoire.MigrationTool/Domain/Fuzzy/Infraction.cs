// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Grimoire.MigrationTool.Domain.Fuzzy;

[Table("infractions")]
internal sealed class Infraction
{
    [Key]
    [Column("oid")]
    public int Id { get; set; }
    [Column("user_id")]
    public ulong UserId { get; set; }
    [Column("user_name")]
    public string UserName { get; set; } = string.Empty;
    [Column("moderator_id")]
    public ulong ModeratorId { get; set; }
    [Column("moderator_name")]
    public string ModeratorName { get; set; } = string.Empty;
    [Column("guild_id")]
    public ulong GuildId { get; set; }
    [Column("reason")]
    public string? Reason { get; set; }
    [Column("infraction_on")]
    public DateTime InfractionOn { get; set; }
    [Column("infraction_type")]
    public string InfractionType { get; set; } = string.Empty;
    public Pardon? Pardon { get; set; }
    public Mute? Mute { get; set; }

}
