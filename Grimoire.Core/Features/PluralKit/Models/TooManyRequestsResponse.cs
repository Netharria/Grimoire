// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Grimoire.Core.Features.PluralKit.Models;
public sealed class TooManyRequestsResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    [JsonPropertyName("retry_after")]
    public int RetryAfter { get; set; }
    [JsonPropertyName("code")]
    public int Code { get; set; }
}
