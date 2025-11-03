// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.Converters;
using DSharpPlus.Commands.Processors.TextCommands;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.CustomCommands;

public class CommandNameArgumentConverter(ILogger<CommandNameArgumentConverter> logger)
    : ITextArgumentConverter<CustomCommandName>, ISlashArgumentConverter<CustomCommandName>
{
    public DiscordApplicationCommandOptionType ParameterType => DiscordApplicationCommandOptionType.String;
    public string ReadableName => "Command Name";
    public ConverterInputType RequiresText => ConverterInputType.Always;

    private readonly ILogger<CommandNameArgumentConverter> _logger = logger;

    public Task<Optional<CustomCommandName>> ConvertAsync(ConverterContext context)
    {
        // This should always be a string since `ISlashArgumentConverter<Ulid>.ParameterType` is
        // `DiscordApplicationCommandOptionType.String`, however we type check here as a safety measure
        // and to provide a more informative log message.
        if (context.Argument is not string value)
        {
            this._logger.LogInformation("Argument is not a string.");
            return Task.FromResult(Optional.FromNoValue<CustomCommandName>());
        }

        if (!value.Contains(' '))
            return Task.FromResult(Optional.FromValue(new CustomCommandName(value)));
        this._logger.LogInformation("Command Name cannot have spaces.");
        return Task.FromResult(Optional.FromNoValue<CustomCommandName>());

    }

}
