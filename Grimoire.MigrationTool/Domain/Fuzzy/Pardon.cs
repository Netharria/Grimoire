// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Grimoire.MigrationTool.Domain.Fuzzy;

[Table("pardons")]
public class Pardon
{
    [Key]
    [Column("infraction_id")]
    [ForeignKey("Infraction")]
    public int Id { get; set; }
    public Infraction Infraction { get; set; } = null!;
    [Column("moderator_id")]
    public ulong ModeratorId { get; set; }
    [Column("moderator_name")]
    public string ModeratorName { get; set; } = string.Empty;
    [Column("pardon_on")]
    public DateTime PardonOn { get; set; }
    [Column("reason")]
    public string? Reason { get; set; }
}
