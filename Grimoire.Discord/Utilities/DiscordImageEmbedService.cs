// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;

namespace Grimoire.Discord.Utilities
{
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
            this._httpClient = httpClientFactory.CreateClient();
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
            foreach ((var url, var index) in urls.Select((x, i) => (x, i)))
            {
                if (string.IsNullOrWhiteSpace(url) || !this._validImageExtensions.Contains(Path.GetExtension(url)))
                    continue;
                var uri = new Uri(url);
                var stream = await this._httpClient.GetStreamAsync(uri);
                var fileName = $"attachment{index}.{Path.GetExtension(uri.AbsolutePath)}";

                var stride = 4 * (index / 4);


                var imageEmbed = new DiscordEmbedBuilder(embed)
                        .WithUrl($"https://discord.com/users/{userId}/{stride}")
                        .WithImageUrl($"attachment://{fileName}");
                if (displayFileNames)
                {
                    var attachments = urls
                        .Skip(stride)
                        .Take(4)
                        .Select(x => $"**{Path.GetFileName(x)}**")
                        .ToArray();
                    imageEmbed.AddMessageTextToFields("Attachments", string.Join("\n", attachments));
                }
                messageBuilder.AddEmbed(imageEmbed);
                messageBuilder.AddFile(fileName, stream);
            }
            if(!messageBuilder.Embeds.Any())
            {
                messageBuilder.AddEmbed(embed);
            }
            return messageBuilder;
        }
    }
}
