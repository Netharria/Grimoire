// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Grimoire.MigrationTool.Domain.Lumberjack
{
    [Table("tracking")]
    public record Tracker
    {
        [Key]
        [Column("userid")]
        public ulong UserId { get; set; }
        [Column("username")]
        public string Username { get; set; } = string.Empty;
        [Column("guildid")]
        public ulong GuildId { get; set; }
        [Column("channelid")]
        public ulong LogChannelId { get; set; }
        [Column("endtime")]
        public DateTimeOffset Endtime { get; set; }
        [Column("modid")]
        public ulong ModeratorId { get; set; }
        [Column("modname")]
        public string ModeratorName { get; set; } = string.Empty;
    }
}
