// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Grimoire.Utilities;

public interface IDiscordImageEmbedService
{
    Task<DiscordMessageBuilder> BuildImageEmbedAsync(string[] urls, UserId userId, DiscordEmbed embed,
        bool displayFileNames = true);
}

/// <summary>
///     Re-uploads a message's images with its log. Discord caps a message at 10 embeds and 6,000 characters across
///     them, so only the first embed carries the log's text and fields, and every list goes there too. The rest only
///     add an image. Embeds sharing a URL show as one gallery of up to four.
/// </summary>
public sealed partial class DiscordImageEmbedService : IDiscordImageEmbedService
{
    /// <summary>Discord's upload limit for a server without boosts. A larger file is refused, losing the whole log.</summary>
    internal const long MaxFileSize = 10 * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly ILogger<DiscordImageEmbedService> _logger;
    private readonly IReadOnlyList<string> _validImageExtensions;

    public DiscordImageEmbedService(IHttpClientFactory httpClientFactory, IConfiguration configuration,
        ILogger<DiscordImageEmbedService> logger)
    {
        this._httpClient = httpClientFactory.CreateClient("Default");
        var validExtensions = configuration.GetValue<string>("validImageExtensions");
        if (string.IsNullOrWhiteSpace(validExtensions))
            throw new ArgumentException("Did not find the configuration for valid extensions");
        this._validImageExtensions = [.. validExtensions.Split(',')];
        this._logger = logger;
    }

    public async Task<DiscordMessageBuilder> BuildImageEmbedAsync(string[] urls, UserId userId, DiscordEmbed embed,
        bool displayFileNames = true)
    {
        var files = urls.Where(url => !string.IsNullOrWhiteSpace(url)).Select(AttachmentRef.Parse).ToArray();
        var imageFiles = files.Where(this.IsImage).ToArray();
        var images = await Task.WhenAll(imageFiles.Select(this.GetImage));
        var downloaded = images.Where(image => image.Content is not null).ToArray();

        var lead = new DiscordEmbedBuilder(embed);
        if (displayFileNames && imageFiles.Length > 0)
            lead.AddMessageTextToFields("Attachments", NameList(imageFiles));
        if (files.Where(file => !this.IsImage(file)).ToArray() is { Length: > 0 } others)
            lead.AddMessageTextToFields("Non-Image Attachments", NameList(others));
        if (images.Where(image => image.Content is null).Select(image => image.File).ToArray() is { Length: > 0 } failed)
            lead.AddMessageTextToFields("Failed to download these images.", NameList(failed));

        var messageBuilder = new DiscordMessageBuilder();
        if (downloaded.Length == 0)
            return messageBuilder.AddEmbed(lead);

        foreach (var (image, index) in downloaded.Select((image, index) => (image, index)))
        {
            var fileName = $"attachment{index}{Path.GetExtension(image.File.Name).ToLowerInvariant()}";
            var imageEmbed = (index == 0 ? lead : new DiscordEmbedBuilder())
                .WithUrl($"https://discord.com/users/{userId}/{4 * (index / 4)}")
                .WithImageUrl($"attachment://{fileName}");
            messageBuilder.AddEmbed(imageEmbed);
            // Reset after sending, so the log publisher's retry uploads the image again.
            messageBuilder.AddFile(fileName, image.Content!, AddFileOptions.ResetStream);
        }

        return messageBuilder;
    }

    private bool IsImage(AttachmentRef file)
        => this._validImageExtensions.Contains(Path.GetExtension(file.Name), StringComparer.OrdinalIgnoreCase);

    private static string NameList(IEnumerable<AttachmentRef> files)
        => string.Join("\n", files.Select(file => $"**{file.Name}**"));

    /// <summary>
    ///     Downloads the whole image now, while its signed URL is fresh, into memory, so it can be uploaded again on a
    ///     retry. Too large a file counts as failed: Discord would refuse the whole message.
    /// </summary>
    private async Task<ImageDownloadResult> GetImage(AttachmentRef file)
    {
        // Rows saved before full URLs were stored hold only the file name, which can't be downloaded.
        if (file.Uri is not { } uri)
            return new ImageDownloadResult(file, null);
        try
        {
            using var response = await this._httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxFileSize)
                return new ImageDownloadResult(file, null);

            await using var body = await response.Content.ReadAsStreamAsync();
            var content = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            // Stops one buffer past the limit, so a body sent without a length can't fill memory.
            while (content.Length <= MaxFileSize && (read = await body.ReadAsync(buffer)) > 0)
                content.Write(buffer, 0, read);
            if (content.Length > MaxFileSize)
                return new ImageDownloadResult(file, null);
            content.Position = 0;
            return new ImageDownloadResult(file, content);
        }
        catch (Exception ex)
        {
            LogDownloadError(this._logger, ex, uri.OriginalString);
            return new ImageDownloadResult(file, null);
        }
    }

    [LoggerMessage(LogLevel.Error, "Was not able to download the image at {url}")]
    static partial void LogDownloadError(ILogger logger, Exception ex, string url);
}

/// <summary>A stored attachment: its file name, and its URL unless only the name was stored.</summary>
internal sealed record AttachmentRef(string Name, Uri? Uri)
{
    /// <summary>The name comes from the path, so the signed query string Discord adds to attachment URLs is left out.</summary>
    public static AttachmentRef Parse(string stored)
        => Uri.TryCreate(stored, UriKind.Absolute, out var uri)
            ? new AttachmentRef(Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath)), uri)
            : new AttachmentRef(stored, null);
}

internal sealed record ImageDownloadResult(AttachmentRef File, MemoryStream? Content);
