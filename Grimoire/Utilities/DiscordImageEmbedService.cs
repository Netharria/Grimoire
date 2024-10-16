// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Grimoire.Utilities;

public interface IDiscordImageEmbedService
{
    Task<DiscordMessageBuilder> BuildImageEmbedAsync(string[] urls, ulong userId, DiscordEmbed embed, bool displayFileNames = true);
}

public sealed partial class DiscordImageEmbedService : IDiscordImageEmbedService
{

    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<string> _validImageExtensions;
    private readonly ILogger<DiscordImageEmbedService> _logger;

    public DiscordImageEmbedService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<DiscordImageEmbedService> logger)
    {
        this._httpClient = httpClientFactory.CreateClient("Default");
        var validExtensions = configuration.GetValue<string>("validImageExtensions");
        if (string.IsNullOrWhiteSpace(validExtensions))
            throw new ArgumentException("Did not find the configuration for valid extensions");
        this._validImageExtensions = [.. validExtensions.Split(',')];
        this._logger = logger;
    }

    public async Task<DiscordMessageBuilder> BuildImageEmbedAsync(string[] urls, ulong userId, DiscordEmbed embed, bool displayFileNames = true)
    {
        var messageBuilder = new DiscordMessageBuilder();

        var imageUrls = this.GetImageurls(urls);

        var images = await this.GetImages(imageUrls);

        foreach ((var image, var index) in images.Where(x => x.Stream is not null).Select((x, i) => (x, i)))
        {
            var fileName = $"attachment{index}.{Path.GetExtension(image.Url)}";
            var stride = 4 * (index / 4);

            var imageEmbed = new DiscordEmbedBuilder(embed)
                    .WithUrl($"https://discord.com/users/{userId}/{stride}")
                    .WithImageUrl($"attachment://{fileName}");

            AddAttachmentFileNames(imageUrls, stride, imageEmbed, displayFileNames);

            messageBuilder.AddEmbed(imageEmbed);

            messageBuilder.AddFile(fileName, image.Stream!);
        }

        this.AddNonImageEmbed(urls, embed, messageBuilder);

        AddImagesThatFailedDownload(images, embed, messageBuilder);

        if (!messageBuilder.Embeds.Any())
        {
            messageBuilder.AddEmbed(embed);
        }

        return messageBuilder;
    }

    private Uri[] GetImageurls(string[] urls)
        => urls
            .Where(url =>
                !string.IsNullOrWhiteSpace(url)
                && this._validImageExtensions.Contains(Path.GetExtension(url.Split('?')[0]), StringComparer.OrdinalIgnoreCase))
            .Select(url => new Uri(url))
            .ToArray();

    private async Task<ImageDownloadResult[]> GetImages(Uri[] uris)
        => await Task.WhenAll(uris.Select(this.GetImage));

    private async Task<ImageDownloadResult> GetImage(Uri uri)
    {
        try
        {
            return new ImageDownloadResult
            {
                Url = uri.AbsolutePath,
                Stream = await this._httpClient.GetStreamAsync(uri)
            };
        }
        catch (Exception ex)
        {
            LogDownloadError(this._logger, ex, uri.OriginalString);
            return new ImageDownloadResult
            {
                Url = uri.AbsolutePath,
            };
        }
    }

    [LoggerMessage(LogLevel.Error, "Was not able to download the image at {url}")]
    public static partial void LogDownloadError(ILogger logger, Exception ex, string url);


    private static void AddAttachmentFileNames(Uri[] imageUrls, int stride, DiscordEmbedBuilder imageEmbed, bool displayFileNames)
    {
        if (displayFileNames)
        {
            var attachments = imageUrls
                    .Skip(stride)
                    .Take(4)
                    .Select(x => $"**{Path.GetFileName(x.AbsolutePath)}**")
                    .ToArray();
            imageEmbed.AddMessageTextToFields("Attachments", string.Join("\n", attachments));
        }
    }

    private void AddNonImageEmbed(string[] urls, DiscordEmbed embed, DiscordMessageBuilder messageBuilder)
    {
        var nonImageAttachements = urls
            .Where(url =>
                !string.IsNullOrWhiteSpace(url)
                && !this._validImageExtensions.Contains(Path.GetExtension(url.Split('?')[0])))
            .Select(x => $"**{Path.GetFileName(x)}**")
            .ToArray(); ;

        if (nonImageAttachements.Length != 0)
        {
            var imageEmbed = new DiscordEmbedBuilder(embed)
                .AddMessageTextToFields("Non-Image Attachments", string.Join("\n", nonImageAttachements));
            messageBuilder.AddEmbed(imageEmbed);
        }
    }

    private static void AddImagesThatFailedDownload(ImageDownloadResult[] urls, DiscordEmbed embed, DiscordMessageBuilder messageBuilder)
    {

        var failedFiles = urls.Where(x => x.Stream is null).Select(x => Path.GetFileName(x.Url)).ToArray();
        if (failedFiles.Length != 0)
        {
            var imageEmbed = new DiscordEmbedBuilder(embed)
                .AddMessageTextToFields("Failed to download these images.", string.Join("\n", failedFiles));
            messageBuilder.AddEmbed(imageEmbed);
        }
    }
}

internal readonly struct ImageDownloadResult
{
    public readonly string Url { get; init; }
    public readonly Stream? Stream { get; init; }

}
