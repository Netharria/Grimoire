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
        => Task.FromResult(context.Argument is string str
            ? CustomCommandName.Create(str).Match(
                FromValue,
                _ => FromNoValue<CustomCommandName>())
            : FromNoValue<CustomCommandName>());
}
