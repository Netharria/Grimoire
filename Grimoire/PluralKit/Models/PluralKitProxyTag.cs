// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Grimoire.PluralKit.Models;
public sealed record PluralKitProxyTag
{
    [JsonPropertyName("prefix")]
    public string? Prefix { get; set; }

    [JsonPropertyName("suffix")]
    public string? Suffix { get; set; }
}
