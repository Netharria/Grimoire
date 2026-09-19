// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;

namespace Grimoire.Tests.Features.Alerts;

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => this._now;

    public void Advance(TimeSpan by) => this._now += by;
}

internal static class AlertTestHelpers
{
    public static Alert Warning(string type = "SlowCommand", string? discriminator = "level", ulong? guild = 1,
        Exception? exception = null)
        => new()
        {
            Severity = AlertSeverity.Warning,
            Type = type,
            Discriminator = discriminator,
            Message = "message",
            Exception = exception,
            GuildId = guild is { } id ? new GuildId(id) : null
        };

    /// <summary>Throws and catches so the exception has a stack trace, like a real one.</summary>
    public static Exception Thrown(Func<Exception> create)
    {
        try
        {
            throw create();
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
