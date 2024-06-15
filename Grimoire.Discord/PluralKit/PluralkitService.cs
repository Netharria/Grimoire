// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Grimoire.Discord.PluralKit.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Grimoire.Discord.PluralKit;

public interface IPluralkitService
{
    ValueTask<PluralKitMessage?> GetProxiedMessageInformation(ulong messageId, DateTimeOffset messageTimestamp);
}

public sealed partial class PluralkitService : IPluralkitService
{
    private readonly IHttpClientFactory _httpClientFactory;

    private const string MESSAGE_ENDPOINT = "messages/";

    private readonly bool _isConfigured;

    public PluralkitService(IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<PluralkitService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _isConfigured = string.IsNullOrWhiteSpace(configuration.GetValue("pluralkitUserAgent", ""))
            || string.IsNullOrWhiteSpace(configuration.GetValue("pluralkitToken", ""));
        PluralkitNotConfiguredLog(logger);
    }


    public async ValueTask<PluralKitMessage?> GetProxiedMessageInformation(ulong messageId, DateTimeOffset messageTimestamp)
    {
        if(!_isConfigured)
        {
            return null;
        }
        if (messageTimestamp.AddSeconds(60) < DateTimeOffset.UtcNow)
        {
            return null;
        }
        var httpClient = _httpClientFactory.CreateClient("Pluralkit");
        try
        {
            return await httpClient.GetFromJsonAsync<PluralKitMessage>(MESSAGE_ENDPOINT + messageId);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    [LoggerMessage(LogLevel.Warning, "UserAgent or Pluralkit Token Not Present. Pluralkit integration disabled.")]
    public static partial void PluralkitNotConfiguredLog(ILogger<PluralkitService> logger);
}
