// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.Converters;
using Grimoire.Features.Moderation;
using NSubstitute;

namespace Grimoire.Tests.Features.Moderation;

/// <summary>
///     The <c>sin_id</c> converter (backlog A-53). <c>SinAdminTests.Pardon_WithAnInvalidId_IsRejected</c> pins 0 and -1
///     through a slash command; these cover the text forms a slash command can't send.
/// </summary>
public sealed class SinIdArgumentConverterTests
{
    private static async Task<DSharpPlus.Entities.Optional<SinId>> ConvertAsync(object? argument)
    {
        var context = Substitute.For<ConverterContext>();
        context.Argument.Returns(argument);
        return await new SinIdArgumentConverter().ConvertAsync(context);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(long.MaxValue)]
    public async Task A_positive_integer_is_a_sin_id(long value)
        => (await ConvertAsync(value)).Value.ShouldBe(new SinId(value));

    [Fact]
    public async Task Positive_integer_text_is_a_sin_id()
        => (await ConvertAsync("42")).Value.ShouldBe(new SinId(42));

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("")]
    [InlineData("99999999999999999999")]
    public async Task Anything_else_is_no_value(string argument)
        => (await ConvertAsync(argument)).HasValue.ShouldBeFalse();

    [Fact]
    public async Task No_argument_is_no_value()
        => (await ConvertAsync(null)).HasValue.ShouldBeFalse();
}
