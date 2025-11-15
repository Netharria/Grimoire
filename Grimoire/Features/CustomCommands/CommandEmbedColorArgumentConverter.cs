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
using static DSharpPlus.Entities.Optional;

namespace Grimoire.Features.CustomCommands;

[UsedImplicitly]
public partial class CommandEmbedColorArgumentConverter
    : ITextArgumentConverter<CustomCommandEmbedColor>, ISlashArgumentConverter<CustomCommandEmbedColor>
{
    public DiscordApplicationCommandOptionType ParameterType => DiscordApplicationCommandOptionType.String;
    public string ReadableName => "Embed Color";
    public ConverterInputType RequiresText => ConverterInputType.Always;

    public Task<Optional<CustomCommandEmbedColor>> ConvertAsync(ConverterContext context) =>
        ConvertToString(context.Argument)
            .Map(value => value.TrimStart('#'))
            .Bind(ValidateHexColor)
            .Map(value => new CustomCommandEmbedColor(value))
            .Map(FromValue)
            .IfFail(FromNoValue<CustomCommandEmbedColor>)
            .AsTask();

    [System.Diagnostics.Contracts.Pure]
    private static Validation<Error, string> ConvertToString(object? value) =>
        value switch
        {
            string s => Success<Error, string>(s),
            _ => Fail<Error, string>(Error.New("Argument is not a valid string."))
        };

    [System.Diagnostics.Contracts.Pure]
    private static Validation<Error, string> ValidateHexColor(string hexColor) =>
        ValidHexColor().IsMatch(hexColor)
            ? Success<Error, string>(hexColor)
            : Fail<Error, string>(Error.New("Argument is not a valid Hex code. Verify it is a 6 digit hex code."));


    [GeneratedRegex(@"[0-9A-Fa-f]{6}\b", RegexOptions.None, 1000)]
    private static partial Regex ValidHexColor();
}
