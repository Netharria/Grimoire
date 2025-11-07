// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

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
public partial class CommandNameArgumentConverter(ILogger<CommandNameArgumentConverter> logger)
    : ITextArgumentConverter<CustomCommandName>, ISlashArgumentConverter<CustomCommandName>
{
    private readonly ILogger<CommandNameArgumentConverter> _logger = logger;
    public DiscordApplicationCommandOptionType ParameterType => DiscordApplicationCommandOptionType.String;
    public string ReadableName => "Command Name";
    public ConverterInputType RequiresText => ConverterInputType.Always;

    public Task<Optional<CustomCommandName>> ConvertAsync(ConverterContext context) =>
        (
            from value in convert<string>(context.Argument)
                .ToFin(Error.New("Argument is not a string."))
            from _1 in guardnot(value.Contains(' '),
                Error.New("Command Name cannot have spaces."))
            select new CustomCommandName(value))
            .Match(
                Succ: name => FromValue(name).AsTask(),
                Fail: _ => FromNoValue<CustomCommandName>().AsTask());


    [LoggerMessage(LogLevel.Information, "Was not able to convert text to Embed Color: {message}")]
    public static partial void LogConversionError(ILogger logger, string message);
}
