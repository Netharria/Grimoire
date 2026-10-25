// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using DSharpPlus.Net.Gateway;
using DSharpPlus.Net.Gateway.Compression;

namespace Grimoire.Tests.Integration.FakeDiscord;

/// <summary>
///     Stands in for the gateway websocket. DSharpPlus's real <c>GatewayClient</c> runs on top of it, so the
///     handshake, event parsing, caching and dispatch to Grimoire's handlers are all the production code paths.
///     Frames are plain JSON: compression lives inside the real transport, which this replaces.
/// </summary>
public sealed class FakeGatewayTransport(FakeGuild guild) : ITransportService
{
    private const int HelloOpCode = 10;
    private const int HeartbeatOpCode = 1;
    private const int HeartbeatAckOpCode = 11;
    private const int IdentifyOpCode = 2;
    private const int ResumeOpCode = 6;
    private const int DispatchOpCode = 0;

    private readonly FakeGuild _guild = guild;
    private readonly ConcurrentQueue<JsonObject> _sentByBot = new();
    private Channel<TransportFrame> _inbound = Channel.CreateUnbounded<TransportFrame>();
    private int _sequence;

    /// <summary>Every payload the bot wrote to the gateway (identify, heartbeats, presence updates).</summary>
    public IReadOnlyCollection<JsonObject> SentByBot => this._sentByBot;

    public void Initialize(string loggerName, IPayloadDecompressor decompressor)
    {
    }

    public ValueTask ConnectAsync(string url)
    {
        Volatile.Write(ref this._inbound, Channel.CreateUnbounded<TransportFrame>());
        this.Enqueue(new JsonObject { ["op"] = HelloOpCode, ["d"] = new JsonObject { ["heartbeat_interval"] = 41_250 } });
        return ValueTask.CompletedTask;
    }

    public async ValueTask<TransportFrame> ReadAsync()
        => await Volatile.Read(ref this._inbound).Reader.ReadAsync();

    public ValueTask WriteAsync(ReadOnlyMemory<byte> payload, WebSocketMessageType messageType = WebSocketMessageType.Text)
    {
        if (JsonNode.Parse(Encoding.UTF8.GetString(payload.Span)) is not JsonObject sent)
            return ValueTask.CompletedTask;

        this._sentByBot.Enqueue(sent);

        switch (sent["op"]?.GetValue<int>())
        {
            case IdentifyOpCode:
                this.Dispatch("READY", this._guild.ReadyPayload());
                this.Dispatch("GUILD_CREATE", this._guild.GuildCreatePayload());
                break;
            case ResumeOpCode:
                this.Dispatch("RESUMED", new JsonObject());
                break;
            case HeartbeatOpCode:
                this.Enqueue(new JsonObject { ["op"] = HeartbeatAckOpCode });
                break;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisconnectAsync(WebSocketCloseStatus closeStatus)
    {
        Volatile.Read(ref this._inbound).Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    /// <summary>Sends a dispatch event (op 0), such as <c>INTERACTION_CREATE</c> or <c>MESSAGE_CREATE</c>, to the bot.</summary>
    public void Dispatch(string eventName, JsonNode data)
        => this.Enqueue(new JsonObject
        {
            ["op"] = DispatchOpCode,
            ["t"] = eventName,
            ["s"] = Interlocked.Increment(ref this._sequence),
            ["d"] = data
        });

    private void Enqueue(JsonObject frame)
        => Volatile.Read(ref this._inbound).Writer.TryWrite(
            new TransportFrame(Encoding.UTF8.GetBytes(frame.ToJsonString()), WebSocketMessageType.Text));

    public void Dispose() => Volatile.Read(ref this._inbound).Writer.TryComplete();
}
