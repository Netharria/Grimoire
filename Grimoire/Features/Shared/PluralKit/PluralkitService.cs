// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using Grimoire.Features.Shared.PluralKit.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.PluralKit;

public interface IPluralkitService
{
    ValueTask<PluralKitMessage?> GetProxiedMessageInformation(ulong messageId, DateTimeOffset messageTimestamp);
}

public sealed partial class PluralkitService : IPluralkitService
{
    private const string MessageEndpoint = "messages/";
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly bool _isConfigured;

    public PluralkitService(IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PluralkitService> logger)
    {
        this._httpClientFactory = httpClientFactory;
        this._isConfigured = !string.IsNullOrWhiteSpace(configuration.GetValue("pluralkitUserAgent", ""))
                             && !string.IsNullOrWhiteSpace(configuration.GetValue("pluralkitToken", ""));
        if (!this._isConfigured)
            PluralkitNotConfiguredLog(logger);
    }


    public async ValueTask<PluralKitMessage?> GetProxiedMessageInformation(ulong messageId,
        DateTimeOffset messageTimestamp)
    {
        if (!this._isConfigured) return null;
        if (messageTimestamp.AddSeconds(60) < DateTimeOffset.UtcNow) return null;
        var httpClient = this._httpClientFactory.CreateClient("Pluralkit");
        try
        {
            return await httpClient.GetFromJsonAsync<PluralKitMessage>(MessageEndpoint + messageId);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound ||
                                              ex.StatusCode == HttpStatusCode.BadGateway)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    [LoggerMessage(LogLevel.Warning, "UserAgent or Pluralkit Token Not Present. Pluralkit integration disabled.")]
    static partial void PluralkitNotConfiguredLog(ILogger<PluralkitService> logger);
}
