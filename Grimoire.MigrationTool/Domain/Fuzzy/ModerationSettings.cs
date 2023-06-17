// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Grimoire.MigrationTool.Domain.Fuzzy
{
    [Table("guilds")]
    public class ModerationSettings
    {
        [Key]
        [Column("id")]
        public ulong Id { get; set; }
        [Column("mod_log")]
        public ulong? ModerationLog { get; set; }
        [Column("public_log")]
        public ulong? PublicBanLog { get; set; }
        [Column("duration_type")]
        public int DurationType { get; set; }
        [Column("duration")]
        public int Duration { get; set; }
        [Column("mute_role")]
        public ulong? MuteRole { get; set; }
    }
}
