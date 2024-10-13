// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Grimoire.Features.Shared.PluralKit.Models;
public sealed record PluralKitMember
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("uuid")]
    public required Guid GUID { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("birthday")]
    public DateOnly? BirthDay { get; set; }

    [JsonPropertyName("pronouns")]
    public string? Pronouns { get; set; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("webhook_avatar_url")]
    public string? WebhookAvatarUrl { get; set; }

    [JsonPropertyName("banner")]
    public string? BannerUrl { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }

    [JsonPropertyName("proxy_tags")]
    public PluralKitProxyTag[] ProxyTag { get; set; } = [];

    [JsonPropertyName("keep_proxy")]
    public required bool KeepProxy { get; set; }

    [JsonPropertyName("tts")]
    public required bool TTS { get; set; }

    [JsonPropertyName("autoproxy_enabled")]
    public bool? AutoproxyEnabled { get; set; }

    [JsonPropertyName("message_count")]
    public int? MessageCount { get; set; }

    [JsonPropertyName("last_message_timestamp")]
    public DateTime? LastMessageTimestamp { get; set; }

    [JsonPropertyName("privacy")]
    public Dictionary<string, string> PrivacySettings { get; set; } = [];
}
