// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Serilog;

namespace Grimoire.Discord.Extensions
{
    public static class DiscordEmbedBuilderExtensions
    {
        public static DiscordEmbedBuilder AddMessageTextToFields(this DiscordEmbedBuilder embedBuilder, string contentType, string? content, bool addBlankField = true, int splitSize = 1024)
        {
            if (string.IsNullOrWhiteSpace(content) && addBlankField)
                return embedBuilder.AddField(contentType, "`blank`");
            if (!string.IsNullOrWhiteSpace(content))
            {
                var splitContent = Regex.Matches(content, @"([\s\S]{1," + splitSize + @"})(?:\s|$)").Select(x => x.Value).ToList();

                if (splitContent.Sum(x => x.Length) != content.Length || splitContent.Any(x => x.Length > 1024))
                {
                    if (splitContent.Any(x => x.Length > 1024))
                        Log.Logger.Warning("Defaulting to crude embed field splitter because the regex returned a string that was longer than {splitSize}. String lengths {lengths}",
                            splitSize, string.Join(' ', splitContent.Select(x => $"{x.Length}")));
                    else
                        Log.Logger.Warning("Defaulting to crude embed field splitter because the regex return left off some characters. Original Length: ({contentLength}), Regex Length: ({regexLength})",
                            content.Length, splitContent.Sum(x => x.Length));
                    splitContent = content.Chunk(splitSize).Select(x => string.Concat(x)).ToList();
                }
                if (splitContent.Any())
                    embedBuilder.AddField(contentType, splitContent[0]);
                if (splitContent.Count > 1)
                    foreach (var x in splitContent.Skip(1))
                        embedBuilder.AddField("**Continued**", x);
            }
            return embedBuilder;
        }
    }
}
