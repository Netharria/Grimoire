// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;

namespace Grimoire.Features.Shared.Alerts;

public enum AlertSeverity
{
    /// <summary>Aggregated into the daily warning report.</summary>
    Warning,

    /// <summary>Posted immediately to the urgent channel.</summary>
    Urgent
}

public sealed record Alert
{
    public required AlertSeverity Severity { get; init; }

    /// <summary>Stable alert category, e.g. <c>SlowCommand</c>.</summary>
    public required string Type { get; init; }

    public required string Message { get; init; }

    /// <summary>Splits one <see cref="Type" /> into separate alerts, e.g. the command name.</summary>
    public string? Discriminator { get; init; }

    public Exception? Exception { get; init; }

    public GuildId? GuildId { get; init; }

    public string Key => AlertKey.Compute(this.Type, this.Discriminator, this.Exception);
}

public static class AlertKey
{
    /// <summary>Identifies "the same problem": type, discriminator, exception type and top Grimoire stack frame.</summary>
    public static string Compute(string type, string? discriminator, Exception? exception)
    {
        var raw = $"{type}|{discriminator}|{exception?.GetType().FullName}|{TopFrame(exception)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16];
    }

    private static string? TopFrame(Exception? exception)
        => exception?.StackTrace?
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith("at Grimoire", StringComparison.Ordinal))
            is { } frame
            ? frame.Split(" in ", 2)[0]
            : null;
}
