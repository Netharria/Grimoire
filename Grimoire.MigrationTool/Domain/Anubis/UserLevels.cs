// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Grimoire.MigrationTool.Domain.Anubis
{
    [Table("user_levels")]
    public record UserLevels
    {
        [Key]
        [Column("rowid")]
        public ulong Id { get; set; }
        [Column("guild_id")]
        public ulong GuildId { get; set; }
        [Column("user_id")]
        public ulong UserId { get; set; }
        [Column("xp")]
        public int Xp { get; set; }
        [Column("timeout")]
        public DateTime Timeout { get; set; }
        [Column("ignore_xp_gain")]
        public bool IgnoredXp { get; set; }
    }
}
