// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;

namespace Grimoire.Tests.Features.Alerts;

public sealed class AlertKeyTests
{
    [Fact]
    public void Same_inputs_produce_the_same_key()
        => AlertKey.Compute("SlowCommand", "level", null).ShouldBe(AlertKey.Compute("SlowCommand", "level", null));

    [Fact]
    public void Different_discriminator_produces_a_different_key()
        => AlertKey.Compute("SlowCommand", "level", null).ShouldNotBe(AlertKey.Compute("SlowCommand", "mute", null));

    [Fact]
    public void Different_type_produces_a_different_key()
        => AlertKey.Compute("SlowCommand", "level", null).ShouldNotBe(AlertKey.Compute("Other", "level", null));

    [Fact]
    public void Different_exception_type_produces_a_different_key()
    {
        var a = AlertTestHelpers.Thrown(() => new InvalidOperationException("x"));
        var b = AlertTestHelpers.Thrown(() => new ArgumentException("x"));

        AlertKey.Compute("T", null, a).ShouldNotBe(AlertKey.Compute("T", null, b));
    }

    [Fact]
    public void Exception_message_does_not_affect_the_key()
        => AlertKey.Compute("T", null, Throw("first")).ShouldBe(AlertKey.Compute("T", null, Throw("second")));

    private static Exception Throw(string message)
        => AlertTestHelpers.Thrown(() => new InvalidOperationException(message));

    [Fact]
    public void Key_is_16_hex_characters()
        => AlertKey.Compute("T", null, null).ShouldMatch("^[0-9A-F]{16}$");
}
