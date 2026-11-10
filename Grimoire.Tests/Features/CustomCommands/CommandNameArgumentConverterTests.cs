// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.Converters;
using NSubstitute;

namespace Grimoire.Tests.Features.CustomCommands;

/// <summary>
///     The custom command <c>name</c> converter (backlog A-53). Name rules are unit-tested in
///     <c>Grimoire.Domain.Test.Unit/CustomCommandNameTests.cs</c>; this checks that the converter applies them.
/// </summary>
public sealed class CommandNameArgumentConverterTests
{
    private static async Task<DSharpPlus.Entities.Optional<CustomCommandName>> ConvertAsync(object? argument)
    {
        var context = Substitute.For<ConverterContext>();
        context.Argument.Returns(argument);
        return await new CommandNameArgumentConverter().ConvertAsync(context);
    }

    [Fact]
    public async Task A_valid_name_converts_to_the_same_name_as_create()
        => (await ConvertAsync("wave")).Value.ShouldBe(CustomCommandName.Create("wave").ShouldSucceed());

    [Theory]
    [InlineData("two words")]
    [InlineData("")]
    public async Task An_invalid_name_is_no_value(string argument)
        => (await ConvertAsync(argument)).HasValue.ShouldBeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData(5)]
    public async Task A_non_string_argument_is_no_value(object? argument)
        => (await ConvertAsync(argument)).HasValue.ShouldBeFalse();
}
