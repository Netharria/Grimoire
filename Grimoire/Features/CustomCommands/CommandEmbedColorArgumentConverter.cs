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
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using static DSharpPlus.Entities.Optional;
using static LanguageExt.Prelude;

namespace Grimoire.Features.CustomCommands;

[UsedImplicitly]
public partial class CommandEmbedColorArgumentConverter(ILogger<CommandEmbedColorArgumentConverter> logger)
    : ITextArgumentConverter<CustomCommandEmbedColor>, ISlashArgumentConverter<CustomCommandEmbedColor>
{
    private readonly ILogger<CommandEmbedColorArgumentConverter> _logger = logger;
    public DiscordApplicationCommandOptionType ParameterType => DiscordApplicationCommandOptionType.String;
    public string ReadableName => "Embed Color";
    public ConverterInputType RequiresText => ConverterInputType.Always;

    public Task<Optional<CustomCommandEmbedColor>> ConvertAsync(ConverterContext context) =>
        (
            from value in convert<string>(context.Argument)
                .Map(value => value.TrimStart('#'))
                .ToFin(Error.New("Argument is not a string"))
            from _1 in guard(ValidHexColor().IsMatch(value),
                Error.New("Argument is not a valid Hex code. Verify it is a 6 digit hex code."))
            select new CustomCommandEmbedColor(value))
        .Match(
            color => FromValue(color).AsTask(),
            _ => FromNoValue<CustomCommandEmbedColor>().AsTask());


    [GeneratedRegex(@"[0-9A-Fa-f]{6}\b", RegexOptions.None, 1000)]
    private static partial Regex ValidHexColor();
}
