// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Grimoire.MigrationTool.Domain.Fuzzy;

[Table("published_messages")]
internal sealed class PublishedMessage
{
    [Key]
    [Column("rowid")]
    public ulong Id { get; set; }
    [Column("infraction_id")]
    public int InfractionId { get; set; }
    [Column("message_id")]
    public ulong MessageId { get; set; }
    [Column("publish_type")]
    public int PublishType { get; set; }
}
