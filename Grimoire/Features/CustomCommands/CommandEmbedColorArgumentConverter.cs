// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using DSharpPlus.Commands.Converters;
using DSharpPlus.Commands.Processors.TextCommands;
using JetBrains.Annotations;
using static DSharpPlus.Entities.Optional;

namespace Grimoire.Features.CustomCommands;

[UsedImplicitly]
public partial class CommandEmbedColorArgumentConverter
    : ITextArgumentConverter<CustomCommandEmbedColor>, ISlashArgumentConverter<CustomCommandEmbedColor>
{
    public DiscordApplicationCommandOptionType ParameterType => DiscordApplicationCommandOptionType.String;
    public string ReadableName => "Embed Color";
    public ConverterInputType RequiresText => ConverterInputType.Always;

    public Task<Optional<CustomCommandEmbedColor>> ConvertAsync(ConverterContext context)
    {
        if (context.Argument is not string raw || string.IsNullOrWhiteSpace(raw))
            return Task.FromResult(FromNoValue<CustomCommandEmbedColor>());
        var str = raw.Trim();

        if(str.StartsWith('#'))
            str = str[1..];
        if (!ValidHexColor().IsMatch(str))
            return Task.FromResult(FromNoValue<CustomCommandEmbedColor>());

        str = str.ToUpperInvariant();

        return Task.FromResult(FromValue(new CustomCommandEmbedColor(str)));
    }


    [GeneratedRegex(@"^[0-9A-Fa-f]{6}$", RegexOptions.None, 1000)]
    private static partial Regex ValidHexColor();
}
