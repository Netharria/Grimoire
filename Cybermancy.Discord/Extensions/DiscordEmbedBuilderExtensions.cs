// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Discord.Enums;
using Cybermancy.Discord.Utilities;
using DSharpPlus.Entities;

namespace Cybermancy.Discord.Extensions
{
    public static class DiscordEmbedBuilderExtensions
    {
        public static DiscordEmbedBuilder AddMessageTextToFields(this DiscordEmbedBuilder embedBuilder, string contentType, string? content, bool addBlankField = true, int splitSize = 1024)
        {
            if (string.IsNullOrWhiteSpace(content) && addBlankField)
                return embedBuilder.AddField(contentType, "`blank`");
            if (!string.IsNullOrWhiteSpace(content))
            {
                var splitContent = content.Chunk(splitSize).Select(x => string.Concat(x)).ToList();
                if (splitContent.Any())
                    embedBuilder.AddField(contentType, splitContent[0]);
                if (splitContent.Count > 1)
                    foreach (var x in splitContent.Skip(1))
                        embedBuilder.AddField("**Continued**", x);
            }
            return embedBuilder;
        }

        public static DiscordEmbedBuilder WithCybermancyColor(this DiscordEmbedBuilder embedBuilder, CybermancyColor cybermancyColor)
            => embedBuilder.WithColor(ColorUtility.GetColor(cybermancyColor));
    }
}
