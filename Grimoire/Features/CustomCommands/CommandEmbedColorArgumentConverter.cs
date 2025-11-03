// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using DSharpPlus.Commands.Converters;
using DSharpPlus.Commands.Processors.TextCommands;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.CustomCommands;

public partial class CommandEmbedColorArgumentConverter(ILogger<CommandEmbedColorArgumentConverter> logger)
    : ITextArgumentConverter<CustomCommandEmbedColor>, ISlashArgumentConverter<CustomCommandEmbedColor>
{
    public DiscordApplicationCommandOptionType ParameterType => DiscordApplicationCommandOptionType.String;
    public string ReadableName => "Embed Color";
    public ConverterInputType RequiresText => ConverterInputType.Always;

    [GeneratedRegex(@"[0-9A-Fa-f]{6}\b", RegexOptions.None, 1000)]
    private static partial Regex ValidHexColor();

    private readonly ILogger<CommandEmbedColorArgumentConverter> _logger = logger;

    public Task<Optional<CustomCommandEmbedColor>> ConvertAsync(ConverterContext context)
    {
        // This should always be a string since `ISlashArgumentConverter<Ulid>.ParameterType` is
        // `DiscordApplicationCommandOptionType.String`, however we type check here as a safety measure
        // and to provide a more informative log message.
        if (context.Argument is not string value)
        {
            this._logger.LogInformation("Argument is not a string.");
            return Task.FromResult(Optional.FromNoValue<CustomCommandEmbedColor>());
        }

        if (ValidHexColor().IsMatch(value))
            return Task.FromResult(Optional.FromValue(new CustomCommandEmbedColor(value)));
        this._logger.LogInformation("Argument is not a valid Hex code.");
        return Task.FromResult(Optional.FromNoValue<CustomCommandEmbedColor>());

    }
}
