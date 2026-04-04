// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.Converters;
using DSharpPlus.Commands.Processors.TextCommands;
using JetBrains.Annotations;
using static DSharpPlus.Entities.Optional;

namespace Grimoire.Features.CustomCommands;

[UsedImplicitly]
public class CommandNameArgumentConverter
    : ITextArgumentConverter<CustomCommandName>, ISlashArgumentConverter<CustomCommandName>
{
    public DiscordApplicationCommandOptionType ParameterType => DiscordApplicationCommandOptionType.String;
    public string ReadableName => "Command Name";
    public ConverterInputType RequiresText => ConverterInputType.Always;

    public Task<Optional<CustomCommandName>> ConvertAsync(ConverterContext context)
    {
        if (context.Argument is not string str || string.IsNullOrWhiteSpace(str))
            return Task.FromResult(FromNoValue<CustomCommandName>());
        str = str.Trim();

        if (str.Any(char.IsWhiteSpace) || str.Length > 24)
            return Task.FromResult(FromNoValue<CustomCommandName>());

        return Task.FromResult(FromValue(new CustomCommandName(str)));
    }
}
