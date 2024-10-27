// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Grimoire.Features.Shared.PluralKit.Models;
public record PluralKitSystem
{
    [JsonPropertyName("id")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public required string Id { get; set; }

    [JsonPropertyName("uuid")]
    public required Guid Guid { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("pronouns")]
    public string? Pronouns { get; set; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("banner")]
    public string? BannerUrl { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }

    [JsonPropertyName("privacy")]
    public Dictionary<string, string> PrivacySettings { get; set; } = [];
}
