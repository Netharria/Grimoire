// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Channels;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Channel = System.Threading.Channels.Channel;

namespace Grimoire.Features.Shared.Channels.GuildLog;

/// <summary>
///     The queue features send guild log messages to. <see cref="GuildLogPublisher" /> reads it and posts each message
///     to its log channel. Register this as a singleton and the publisher as a hosted service. The publisher receives
///     this same instance through injection, so there is only ever one queue.
/// </summary>
/// <remarks>
///     The log gate (Q26b, LOG-2) lives here, so no log skips it: <see cref="IsLoggedAsync" /> says whether anyone will
///     see a log, and the gated overload builds one only then. Ported code reaches both through
///     <c>GuildLogs&lt;RT&gt;.Post</c>.
/// </remarks>
public sealed class GuildLog(SettingsModule settingsModule, TimeProvider timeProvider)
{
    private const int QueueCapacity = 1024;

    private readonly Channel<GuildLogMessage> _channel =
        Channel.CreateBounded<GuildLogMessage>(new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait // backpressure instead of unbounded memory growth
        });

    internal ChannelReader<GuildLogMessage> Reader => this._channel.Reader;

    /// <summary>Queues a finished message.</summary>
    public ValueTask EnqueueAsync(GuildLogMessage message, CancellationToken cancellationToken = default)
        => this._channel.Writer.WriteAsync(message, cancellationToken);

    /// <summary>
    ///     The bridge for code not yet ported: the old message is converted and stamped with the time it's queued,
    ///     which is when its event was handled (defect 27).
    /// </summary>
    public ValueTask SendLogMessageAsync(GuildLogMessageBase message, CancellationToken cancellationToken = default)
        => this.EnqueueAsync(message.ToMessage(timeProvider.GetUtcNow()), cancellationToken);

    /// <summary>
    ///     Builds and queues the log only when someone will see it. <paramref name="build" /> runs on the caller, never
    ///     in the publisher, whose single reader would otherwise wait on every avatar and image download (LOG-2).
    /// </summary>
    public async ValueTask SendLogMessageAsync(GuildId guildId, GuildLogType type,
        Func<ValueTask<GuildLogMessageBase>> build, CancellationToken cancellationToken = default)
    {
        if (await this.IsLoggedAsync(guildId, type, cancellationToken))
            await this.SendLogMessageAsync(await build(), cancellationToken);
    }

    /// <summary>
    ///     Whether a log of this type would be posted: its module is on and it has a channel. Both are cache hits. A
    ///     failed settings lookup counts as no: the publisher would make the same lookup and drop the log too.
    /// </summary>
    public async Task<bool> IsLoggedAsync(GuildId guildId, GuildLogType type,
        CancellationToken cancellationToken = default)
        => (await settingsModule.IsModuleEnabled(type.GetLogTypeModule(), guildId, cancellationToken))
           // The gate's own rule (spec 1.4, section 3.9), not a default hiding a failure: see the summary.
           .Match(Succ: enabled => enabled, Fail: _ => false)
           && await settingsModule.IsLogChannelSet(type, guildId, cancellationToken);

    /// <summary>
    ///     Rejects further messages. The publisher calls this when the host stops. Messages still queued at that
    ///     point are not posted, because stopping also cancels the publisher's read loop.
    /// </summary>
    internal void Complete() => this._channel.Writer.TryComplete();
}
