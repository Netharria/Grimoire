// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grimoire.Features.Shared.PluralKit.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;

namespace Grimoire.Features.Shared.PluralKit;

public interface IPluralkitService
{
    ValueTask<PluralKitMessage?> GetProxiedMessageInformation(ulong messageId, DateTimeOffset messageTimestamp);
}

/// <remarks>A singleton: DSharpPlus makes a scope per event, so a scoped one would log its warning on every delete.</remarks>
public sealed partial class PluralkitService : IPluralkitService
{
    private const string MessageEndpoint = "messages/";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly bool _isConfigured;
    private readonly ILogger<PluralkitService> _logger;

    public PluralkitService(IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PluralkitService> logger)
    {
        this._httpClientFactory = httpClientFactory;
        this._logger = logger;
        this._isConfigured = !string.IsNullOrWhiteSpace(configuration.GetValue("pluralkitUserAgent", ""))
                             && !string.IsNullOrWhiteSpace(configuration.GetValue("pluralkitToken", ""));
        if (!this._isConfigured)
            PluralkitNotConfiguredLog(logger);
    }

    /// <summary>
    ///     The message as PluralKit knows it, or null if it doesn't or can't be asked. The lookup is optional, so no
    ///     failure escapes: the caller logs the message without the PluralKit details instead.
    /// </summary>
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
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
        // The pipeline's timeout, rate limiter and circuit breaker all throw ExecutionRejectedException; retries that
        // run out throw HttpRequestException; a changed response shape throws JsonException. No token is passed, so a
        // cancellation can only be HttpClient's own timeout.
        catch (Exception ex) when (ex is HttpRequestException or ExecutionRejectedException or JsonException
                                       or TaskCanceledException)
        {
            LogLookupFailed(this._logger, ex, messageId);
            return null;
        }
    }

    [LoggerMessage(LogLevel.Warning, "UserAgent or Pluralkit Token Not Present. Pluralkit integration disabled.")]
    static partial void PluralkitNotConfiguredLog(ILogger<PluralkitService> logger);

    [LoggerMessage(LogLevel.Information, "PluralKit lookup for message {MessageId} failed, so it's handled as an ordinary message")]
    static partial void LogLookupFailed(ILogger logger, Exception ex, ulong messageId);
}
