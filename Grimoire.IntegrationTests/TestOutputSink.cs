// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Grimoire.IntegrationTests;

/// <summary>
///     Writes the test bot's log lines to the current test's output, so a failing test shows them.
/// </summary>
/// <remarks>
///     This replaces Serilog's console sink with <c>[assembly: CaptureConsole]</c>. That pair deadlocked when xunit ran
///     with its JSON console reporter (the mode vstest and Stryker use): a log write held the console lock while waiting
///     for xunit's logger lock, and xunit held its lock while waiting for the console (spec 0.4, section 2a). Lines
///     written outside a test, for example during fixture setup, have no output to go to and are dropped here;
///     <see cref="TestLogSink" /> still records them.
/// </remarks>
public sealed class TestOutputSink : ILogEventSink
{
    private readonly MessageTemplateTextFormatter _formatter = new(
        "[{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}", CultureInfo.InvariantCulture);

    public void Emit(LogEvent logEvent)
    {
        if (TestContext.Current.TestOutputHelper is not { } output)
            return;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        this._formatter.Format(logEvent, writer);
        try
        {
            output.Write(writer.ToString());
        }
        catch (InvalidOperationException)
        {
            // The test has finished; its output can no longer be written to.
        }
    }
}
