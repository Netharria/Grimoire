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
    [Table("lumberjack_messages")]
    public record LumberjackMessage
    {
        [Key]
        [Column("message_id")]
        public ulong MessageId { get; set; }
        [Column("channel_id")]
        public ulong ChannelId { get; set; }
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }
    }
}
