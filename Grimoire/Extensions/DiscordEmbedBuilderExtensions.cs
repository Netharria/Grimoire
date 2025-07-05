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

    public static DiscordEmbedBuilder AddMessageTextToFields(this DiscordEmbedBuilder embedBuilder, string contentType,
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

        if (splitContent.Any(x => x.Length > 1024))
        {
            Log.Logger.Warning("Size of element is too large. Trying trim.");
            splitContent = splitContent.Select(x => x.Trim()).ToList();

            if (splitContent.Any(x => x.Length > 1024))
            {
                Log.Logger.Warning(
                    "Defaulting to crude embed field splitter because the regex returned a string that was longer than 1024. String lengths {lengths}",
                    string.Join(' ', splitContent.Select(x => x.Length)));
                splitContent = content.Chunk(1024).Select(x => string.Concat(x)).ToList();
            }
        }

        if (splitContent.Count > 0)
            embedBuilder.AddField(contentType, splitContent[0]);
        if (splitContent.Count <= 1)
            return embedBuilder;

        foreach (var x in splitContent.Skip(1))
            embedBuilder.AddField("**Continued**", x);

        return embedBuilder;
    }
}
