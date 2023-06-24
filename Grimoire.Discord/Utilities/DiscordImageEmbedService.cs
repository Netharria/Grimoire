// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;

namespace Grimoire.Discord.Utilities;

public interface IDiscordImageEmbedService
{
    Task<DiscordMessageBuilder> BuildImageEmbedAsync(AttachmentDto[] attachmentDtos, ulong userId, ulong channelId, DiscordEmbed embed, bool displayFileNames = true);
    Task<DiscordMessageBuilder> BuildImageEmbedAsync(string[] urls, ulong userId, DiscordEmbed embed, bool displayFileNames = true);
}

public class DiscordImageEmbedService : IDiscordImageEmbedService
{

    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<string> _validImageExtensions;

    public DiscordImageEmbedService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        this._httpClient = httpClientFactory.CreateClient("Default");
        var validExtensions = configuration.GetValue<string>("validImageExtensions");
        if (string.IsNullOrWhiteSpace(validExtensions))
            throw new ArgumentException("Did not find the configuration for valid extensions");
        this._validImageExtensions = validExtensions.Split(',').ToImmutableList();
    }

    public Task<DiscordMessageBuilder> BuildImageEmbedAsync(AttachmentDto[] attachmentDtos, ulong userId, ulong channelId, DiscordEmbed embed, bool displayFileNames = true)
        => this.BuildImageEmbedAsync(attachmentDtos.Select(attachment
            => Path.Combine("https://cdn.discordapp.com/attachments/", channelId.ToString(), attachment.Id.ToString(), attachment.FileName)).ToArray(),
                userId, embed);

    public async Task<DiscordMessageBuilder> BuildImageEmbedAsync(string[] urls, ulong userId, DiscordEmbed embed, bool displayFileNames = true)
    {
        var messageBuilder = new DiscordMessageBuilder();

        var imageUrls = this.GetImageurls(urls);

        var images = await this.GetImages(imageUrls);

        foreach ((var url, var index) in imageUrls.Select((x, i) => (x, i)))
        {
            var fileName = $"attachment{index}.{Path.GetExtension(url.AbsolutePath)}";
            var stride = 4 * (index / 4);

            var imageEmbed = new DiscordEmbedBuilder(embed)
                    .WithUrl($"https://discord.com/users/{userId}/{stride}")
                    .WithImageUrl($"attachment://{fileName}");

            AddAttachmentFileNames(imageUrls, stride, imageEmbed, displayFileNames);

            messageBuilder.AddEmbed(imageEmbed);

            var stream = images[index];
            messageBuilder.AddFile(fileName, stream);
        }

        this.AddNonImageEmbed(urls, embed, messageBuilder);

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
                && this._validImageExtensions.Contains(Path.GetExtension(url.Split('?')[0])))
            .Select(url => new Uri(url))
            .ToArray();

    private Task<Stream[]> GetImages(Uri[] uris)
        => Task.WhenAll(
            uris.Select(this._httpClient.GetStreamAsync));

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

        if (nonImageAttachements.Any())
        {
            var imageEmbed = new DiscordEmbedBuilder(embed)
                .AddMessageTextToFields("Non-Image Attachments", string.Join("\n", nonImageAttachements));
            messageBuilder.AddEmbed(imageEmbed);
        }
    }
}
