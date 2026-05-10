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
    public required AttachmentFileName FileName { get; init; }
    public required AttachmentId Id { get; init; }
}

public readonly record struct AttachmentFileName
{
    private AttachmentFileName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    internal static AttachmentFileName FromDatabase(string value) => new(value);

    public static Validation<AttachmentFileName> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<AttachmentFileName>.Fail(new Error("attachment-file-name.empty",
                "Attachment file name cannot be empty."));
        return Validation<AttachmentFileName>.Succeed(new AttachmentFileName(value));
    }

    public override string ToString() => Value;
}
