// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Grimoire.MigrationTool.Domain.Fuzzy;

[Table("mutes")]
public class Mute
{
    [Key]
    [Column("infraction_id")]
    public int Id { get; set; }
    [ForeignKey("Id")]
    public Infraction Infraction { get; set; } = null!;
    [Column("end_time")]
    public DateTime EndTime { get; set; }
    [Column("user_id")]
    public ulong UserId { get; set; }
    [Column("user_name")]
    public string UserName { get; set; } = string.Empty;
}
