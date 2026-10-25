// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace Grimoire.Tests.Integration.FakeDiscord;

/// <summary>
///     Replaces the bot's "Default" HttpClient, which downloads avatars and attachments to re-upload them in logs. It
///     answers every GET with a tiny PNG and records the URL, so tests never reach the real network.
/// </summary>
public sealed class FakeCdn : HttpMessageHandler
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private readonly ConcurrentQueue<Uri> _downloads = new();

    public IReadOnlyCollection<Uri> Downloads => this._downloads;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        this._downloads.Enqueue(request.RequestUri!);
        var content = new ByteArrayContent(OnePixelPng);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
