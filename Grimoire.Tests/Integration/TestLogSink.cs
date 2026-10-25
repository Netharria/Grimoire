// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Serilog.Core;
using Serilog.Events;

namespace Grimoire.Tests.Integration;

/// <summary>Collects every log event the bot writes so tests can wait on startup and assert nothing errored.</summary>
public sealed class TestLogSink : ILogEventSink
{
    private readonly Lock _lock = new();
    private readonly List<LogEvent> _events = [];
    private readonly List<(Func<LogEvent, bool> Match, TaskCompletionSource<LogEvent> Done)> _waiters = [];

    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            lock (this._lock)
                return [.. this._events];
        }
    }

    public IReadOnlyList<string> Errors
        => [.. this.Events
            .Where(logEvent => logEvent.Level >= LogEventLevel.Error)
            .Select(logEvent => $"{logEvent.RenderMessage()} {logEvent.Exception}")];

    public void Emit(LogEvent logEvent)
    {
        lock (this._lock)
        {
            this._events.Add(logEvent);
            foreach (var waiter in this._waiters.Where(waiter => waiter.Match(logEvent)).ToArray())
            {
                this._waiters.Remove(waiter);
                waiter.Done.TrySetResult(logEvent);
            }
        }
    }

    public Task<LogEvent> WaitForAsync(Func<LogEvent, bool> match, TimeSpan timeout)
    {
        TaskCompletionSource<LogEvent> done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (this._lock)
        {
            if (this._events.FirstOrDefault(match) is { } existing)
                return Task.FromResult(existing);
            this._waiters.Add((match, done));
        }

        return done.Task.WaitAsync(timeout);
    }
}
