// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Globalization;
using DSharpPlus.Commands.Converters;
using DSharpPlus.Commands.Processors.TextCommands;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation;

public class SinIdArgumentConverter(ILogger<SinIdArgumentConverter> logger)
    : ITextArgumentConverter<SinId>, ISlashArgumentConverter<SinId>
{
    public DiscordApplicationCommandOptionType ParameterType => DiscordApplicationCommandOptionType.String;
    public string ReadableName => "Command Name";
    public ConverterInputType RequiresText => ConverterInputType.Always;

    private readonly ILogger<SinIdArgumentConverter> _logger = logger;

    public Task<Optional<SinId>> ConvertAsync(ConverterContext context) =>
    long.TryParse(context.Argument?.ToString(), CultureInfo.InvariantCulture, out var result)
        && result > 0
    ? Task.FromResult(Optional.FromValue(new SinId(result)))
    : Task.FromResult(Optional.FromNoValue<SinId>());

}
