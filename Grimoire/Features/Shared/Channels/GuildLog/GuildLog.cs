// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Threading.Channels;
using Channel = System.Threading.Channels.Channel;

namespace Grimoire.Features.Shared.Channels.GuildLog;

/// <summary>
///     The queue features send guild log messages to. <see cref="GuildLogPublisher" /> reads it and posts each message
///     to its log channel. Register this as a singleton and the publisher as a hosted service. The publisher receives
///     this same instance through injection, so there is only ever one queue.
/// </summary>
public sealed class GuildLog
{
    private const int QueueCapacity = 1024;

    private readonly Channel<GuildLogMessageBase> _channel =
        Channel.CreateBounded<GuildLogMessageBase>(new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait // backpressure instead of unbounded memory growth
        });

    internal ChannelReader<GuildLogMessageBase> Reader => this._channel.Reader;

    public ValueTask SendLogMessageAsync(
        GuildLogMessageBase logMessageMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logMessageMessage);

        return this._channel.Writer.TryWrite(logMessageMessage)
            ? ValueTask.CompletedTask
            : this._channel.Writer.WriteAsync(logMessageMessage, cancellationToken);
    }

    /// <summary>
    ///     Rejects further messages. The publisher calls this when the host stops. Messages still queued at that
    ///     point are not posted, because stopping also cancels the publisher's read loop.
    /// </summary>
    internal void Complete() => this._channel.Writer.TryComplete();
}
