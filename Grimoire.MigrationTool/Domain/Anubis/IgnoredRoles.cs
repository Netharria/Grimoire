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
    [Table("ignored_roles")]
    public record IgnoredRoles
    {
        [Key]
        [Column("role_id")]
        public ulong RoleId { get; set; }
        [Column("guild_id")]
        public ulong GuildId { get; set; }
    }
}
