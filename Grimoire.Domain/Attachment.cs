// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed record Attachment
{
    public required MessageId MessageId { get; init; }
    public Message? Message { get; init; }
    /// <summary>The attachment's full signed URL, which the delete log downloads the file from.</summary>
    public required AttachmentUrl AttachmentUrl { get; init; }
    public required AttachmentId Id { get; init; }
}

/// <summary>
///     An attachment's full URL, signed by Discord, so the file can be downloaded later. Rows saved before full URLs
///     were stored hold only the file name.
/// </summary>
public readonly record struct AttachmentUrl
{
    private AttachmentUrl(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>
    ///     Stored rows and Discord's own data are both trusted as they come, so there's nothing to validate.
    /// </summary>
    public static AttachmentUrl Create(string value) => new(value);

    public override string ToString() => Value;
}
