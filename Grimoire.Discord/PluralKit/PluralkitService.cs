// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Grimoire.Discord.PluralKit.Models;

namespace Grimoire.Discord.PluralKit;

public interface IPluralkitService
{
    ValueTask<PluralKitMessage?> GetProxiedMessageInformation(ulong messageId, DateTimeOffset messageTimestamp);
}

public sealed class PluralkitService(IHttpClientFactory httpClientFactory) : IPluralkitService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    private const string MESSAGE_ENDPOINT = "messages/";


    public async ValueTask<PluralKitMessage?> GetProxiedMessageInformation(ulong messageId, DateTimeOffset messageTimestamp)
    {
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
}
