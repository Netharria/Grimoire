// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Serilog;

namespace Grimoire.Extensions;

public static partial class DiscordEmbedBuilderExtensions
{
    [GeneratedRegex(@"([\s\S]{1,1024})(?:\s|$)", RegexOptions.None, 1000)]
    private static partial Regex SplitText();

    extension(DiscordEmbedBuilder embedBuilder)
    {
        public DiscordEmbedBuilder AddMessageTextToFields(string contentType,
            string? content, bool addBlankField = true)
        {
            if (string.IsNullOrWhiteSpace(content) && addBlankField)
                return embedBuilder.AddField(contentType, "`blank`");
            if (string.IsNullOrWhiteSpace(content)) return embedBuilder;
            var splitContent = SplitText().Matches(content).Select(x => x.Value).ToList();

            if (splitContent.Sum(x => x.Length) != content.Length)
            {
                Log.Logger.Warning(
                    "Defaulting to crude embed field splitter because the regex return left off some characters. Original Length: ({contentLength}), Regex Length: ({regexLength})",
                    content.Length, splitContent.Sum(x => x.Length));
                splitContent = content.Chunk(1024).Select(x => string.Concat(x)).ToList();
            }

            // A match keeps the whitespace it split on, so a full one is 1025 long until that's trimmed off. A chunk
            // that is only whitespace, as a long run of spaces gives, would make AddField throw and lose the log.
            splitContent = splitContent
                .Select(x => x.Length > 1024 ? x.TrimEnd() : x)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (splitContent.Count > 0)
                embedBuilder.AddField(contentType, splitContent[0]);
            if (splitContent.Count <= 1)
                return embedBuilder;

            foreach (var x in splitContent.Skip(1))
                embedBuilder.AddField("**Continued**", x);

            return embedBuilder;
        }

        public DiscordEmbedBuilder WithThumbnail(AvatarFileName avatarFileName)
            => embedBuilder.WithThumbnail(avatarFileName.Value);
    }
}
