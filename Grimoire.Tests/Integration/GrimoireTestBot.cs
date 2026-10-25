// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json.Nodes;
using DSharpPlus;
using DSharpPlus.Net;
using DSharpPlus.Net.Gateway;
using Grimoire.Features.Leveling;
using Grimoire.Features.LogCleanup;
using Grimoire.Features.Moderation.Lock;
using Grimoire.Features.Moderation.Mute;
using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Gateway;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Grimoire.Tests.Integration;

/// <summary>
///     The real Grimoire host, built by <c>AddGrimoire</c> exactly as in production, with only the two ends of the
///     Discord connection replaced: the gateway websocket and the REST HTTP handler. Everything in between runs for
///     real, including command routing, context checks, settings and their cache, EF Core, and the Postgres
///     database from <see cref="GrimoireCoreFactory" />.
/// </summary>
public sealed class GrimoireTestBot : IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Timer-driven background services. They are left out so a test only sees the work its own events cause.
    ///     A test for one of them can start it explicitly from <see cref="Services" />.
    /// </summary>
    private static readonly Type[] ScheduledServices =
    [
        typeof(GatewayWatchdogService),
        typeof(WarningFlushService),
        typeof(DailyWarningReportService),
        typeof(CleanupLogsBackgroundTask),
        typeof(LockBackgroundTasks),
        typeof(MuteBackgroundTasks),
        typeof(LeaderboardRefreshBackgroundTask)
    ];

    private readonly IHost _host;
    private int _interactionCount;
    private long _snowflakeCount;

    private GrimoireTestBot(IHost host, FakeGuild guild, FakeGatewayTransport gateway, FakeDiscordRest rest,
        TestLogSink logs)
    {
        this._host = host;
        this.Guild = guild;
        this.Gateway = gateway;
        this.Rest = rest;
        this.Logs = logs;
    }

    public FakeGuild Guild { get; }
    public FakeGatewayTransport Gateway { get; }
    public FakeDiscordRest Rest { get; }
    public TestLogSink Logs { get; }
    public IServiceProvider Services => this._host.Services;

    public async ValueTask DisposeAsync()
    {
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await this._host.StopAsync(stopping.Token);
        this._host.Dispose();
    }

    /// <param name="database">The shared Postgres fixture. Reset it before starting a bot.</param>
    public static async Task<GrimoireTestBot> StartAsync(GrimoireCoreFactory database)
    {
        var guild = new FakeGuild();

        var gateway = new FakeGatewayTransport(guild);
        var rest = new FakeDiscordRest(guild);
        var logs = new TestLogSink();

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["token"] = "fake.discord.token",
            ["ConnectionStrings:Grimoire"] = database.ConnectionString,
            ["pluralkitApiEndpoint"] = "http://pluralkit.fake.invalid/",
            ["pluralkitUserAgent"] = "Grimoire.Tests"
        });

        builder.Services.AddGrimoire(builder.Configuration);

        // The two seams: DSharpPlus resolves its websocket transport and its REST HttpClient from DI.
        builder.Services.Replace(ServiceDescriptor.Singleton<ITransportService>(gateway));
        builder.Services.AddHttpClient("DSharpPlus.Rest.HttpClient")
            .ConfigurePrimaryHttpMessageHandler(() => rest);

        // The client-side REST throttle only slows startup's ~30 command registrations against a fake.
        builder.Services.Configure<RestClientOptions>(options => options.MaximumConcurrentRestRequests = 1_000);

        builder.Services.RemoveAll<ILoggerFactory>();
        builder.Services.AddSerilog(logger => logger
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
                .WriteTo.Sink(logs)
                .WriteTo.Console(
                    outputTemplate: "[{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture),
            preserveStaticLogger: true);

        foreach (var scheduled in builder.Services
                     .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                                          && ScheduledServices.Contains(descriptor.ImplementationType))
                     .ToArray())
            builder.Services.Remove(scheduled);

        var host = builder.Build();
        var bot = new GrimoireTestBot(host, guild, gateway, rest, logs);

        try
        {
            using var starting = new CancellationTokenSource(StartupTimeout);
            await host.StartAsync(starting.Token);
            await bot.WaitUntilReadyAsync();
        }
        catch
        {
            await bot.DisposeAsync();
            throw;
        }

        return bot;
    }

    /// <summary>
    ///     Ready means the guild is cached and slash commands are mapped. DSharpPlus logs the mapping once it is
    ///     built, and that line is the only signal that comes after it.
    /// </summary>
    private async Task WaitUntilReadyAsync()
    {
        await this.Logs.WaitForAsync(logEvent => logEvent.MessageTemplate.Text.Contains(
            "top-level slash commands", StringComparison.Ordinal), StartupTimeout);

        var client = this.Services.GetRequiredService<DiscordClient>();
        using var timeout = new CancellationTokenSource(StartupTimeout);
        while (!client.Guilds.ContainsKey(this.Guild.Id))
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
    }

    /// <summary>Turns on a module and points its log channel at the guild's mod-log channel.</summary>
    public async Task ConfigureModerationAsync()
    {
        var settings = this.Services.GetRequiredService<SettingsModule>();
        var guildId = new GuildId(this.Guild.Id);
        var moderatorId = new ModeratorId(this.Guild.Owner.Id);

        (await settings.SetModuleState(Module.Moderation, guildId, moderatorId, true)).ShouldSucceed();
        (await settings.SetLogChannelSetting(GuildLogType.Moderation, guildId, moderatorId,
            new ChannelId(this.Guild.ModLog.Id))).ShouldSucceed();
    }

    /// <summary>Turns on the message log, which logs every channel unless an override says otherwise.</summary>
    public async Task ConfigureMessageLogAsync()
        => (await this.Services.GetRequiredService<SettingsModule>().SetModuleState(Module.MessageLog,
            new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id), true)).ShouldSucceed();

    /// <summary>
    ///     Sends a <c>MESSAGE_CREATE</c> and returns the message id. Ids are minted from the current time because the
    ///     bot dates a message by its snowflake, and the log cleanup purges messages by that date.
    /// </summary>
    public ulong SendMessage(FakeUser author, FakeChannel channel, string content)
    {
        var messageId = this.NextSnowflake();
        this.Gateway.Dispatch("MESSAGE_CREATE", new JsonObject
        {
            ["id"] = FakeUser.Snowflake(messageId),
            ["channel_id"] = FakeUser.Snowflake(channel.Id),
            ["guild_id"] = FakeUser.Snowflake(this.Guild.Id),
            ["author"] = author.ToJson(),
            ["member"] = FakeGuild.MemberJson(this.Guild.GetMember(author), false),
            ["content"] = content,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            ["edited_timestamp"] = null,
            ["tts"] = false,
            ["mention_everyone"] = false,
            ["mentions"] = new JsonArray(),
            ["mention_roles"] = new JsonArray(),
            ["attachments"] = new JsonArray(),
            ["embeds"] = new JsonArray(),
            ["components"] = new JsonArray(),
            ["pinned"] = false,
            ["type"] = 0,
            ["flags"] = 0
        });
        return messageId;
    }

    public void AddReaction(FakeUser user, FakeChannel channel, ulong messageId, FakeEmoji emoji)
        => this.Gateway.Dispatch("MESSAGE_REACTION_ADD", new JsonObject
        {
            ["user_id"] = FakeUser.Snowflake(user.Id),
            ["channel_id"] = FakeUser.Snowflake(channel.Id),
            ["message_id"] = FakeUser.Snowflake(messageId),
            ["guild_id"] = FakeUser.Snowflake(this.Guild.Id),
            ["member"] = FakeGuild.MemberJson(this.Guild.GetMember(user)),
            ["emoji"] = emoji.ToJson(),
            ["burst"] = false,
            ["type"] = 0
        });

    public void RemoveReaction(FakeUser user, FakeChannel channel, ulong messageId, FakeEmoji emoji)
        => this.Gateway.Dispatch("MESSAGE_REACTION_REMOVE", new JsonObject
        {
            ["user_id"] = FakeUser.Snowflake(user.Id),
            ["channel_id"] = FakeUser.Snowflake(channel.Id),
            ["message_id"] = FakeUser.Snowflake(messageId),
            ["guild_id"] = FakeUser.Snowflake(this.Guild.Id),
            ["emoji"] = emoji.ToJson(),
            ["burst"] = false,
            ["type"] = 0
        });

    /// <summary>A moderator removing one emoji from a message for everyone.</summary>
    public void RemoveEmoji(FakeChannel channel, ulong messageId, FakeEmoji emoji)
        => this.Gateway.Dispatch("MESSAGE_REACTION_REMOVE_EMOJI", new JsonObject
        {
            ["channel_id"] = FakeUser.Snowflake(channel.Id),
            ["message_id"] = FakeUser.Snowflake(messageId),
            ["guild_id"] = FakeUser.Snowflake(this.Guild.Id),
            ["emoji"] = emoji.ToJson()
        });

    public void RemoveAllReactions(FakeChannel channel, ulong messageId)
        => this.Gateway.Dispatch("MESSAGE_REACTION_REMOVE_ALL", new JsonObject
        {
            ["channel_id"] = FakeUser.Snowflake(channel.Id),
            ["message_id"] = FakeUser.Snowflake(messageId),
            ["guild_id"] = FakeUser.Snowflake(this.Guild.Id)
        });

    private ulong NextSnowflake()
    {
        const long discordEpochMilliseconds = 1_420_070_400_000;
        var sinceEpoch = (ulong)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - discordEpochMilliseconds);
        return (sinceEpoch << 22) | (ulong)(Interlocked.Increment(ref this._snowflakeCount) & 0xFFF);
    }

    /// <summary>
    ///     Gateway events are handled in the background, so a test polls until the bot's write is visible.
    ///     Returns the last value read so the assertion that follows reports what was actually there.
    /// </summary>
    public static async Task<T> EventuallyAsync<T>(Func<Task<T>> read, Func<T, bool> isDone, TimeSpan? timeout = null)
    {
        using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
        var value = await read();
        while (!isDone(value) && !deadline.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25), CancellationToken.None);
            value = await read();
        }

        return value;
    }

    /// <summary>Sends an <c>INTERACTION_CREATE</c> for a slash command, as Discord does when a member runs it.</summary>
    public FakeInteraction RunSlashCommand(string commandName, FakeUser invoker, params SlashOption[] options)
    {
        var number = Interlocked.Increment(ref this._interactionCount);
        var interaction = new FakeInteraction(1_400_000_000_000_000_000UL + (ulong)number, $"fake-interaction-token-{number}");
        var member = this.Guild.GetMember(invoker);
        var memberJson = FakeGuild.MemberJson(member);
        memberJson["permissions"] = this.Guild.GuildPermissionsOf(invoker).ToString(CultureInfo.InvariantCulture);

        var resolvedUsers = new JsonObject();
        var resolvedMembers = new JsonObject();
        foreach (var user in options.Select(option => option.ResolvedUser).OfType<FakeUser>())
        {
            resolvedUsers[FakeUser.Snowflake(user.Id)] = user.ToJson();
            if (this.Guild.Members.FirstOrDefault(m => m.User.Id == user.Id) is { } resolvedMember)
                resolvedMembers[FakeUser.Snowflake(user.Id)] = FakeGuild.MemberJson(resolvedMember, false);
        }

        this.Gateway.Dispatch("INTERACTION_CREATE", new JsonObject
        {
            ["id"] = FakeUser.Snowflake(interaction.Id),
            ["application_id"] = FakeUser.Snowflake(this.Guild.ApplicationId),
            ["type"] = 2,
            ["token"] = interaction.Token,
            ["version"] = 1,
            ["guild_id"] = FakeUser.Snowflake(this.Guild.Id),
            ["channel_id"] = FakeUser.Snowflake(this.Guild.General.Id),
            ["channel"] = this.Guild.ChannelJson(this.Guild.General),
            ["member"] = memberJson,
            ["app_permissions"] = FakeGuild.AdministratorPermission.ToString(CultureInfo.InvariantCulture),
            ["locale"] = "en-US",
            ["guild_locale"] = "en-US",
            ["entitlements"] = new JsonArray(),
            ["authorizing_integration_owners"] = new JsonObject { ["0"] = FakeUser.Snowflake(this.Guild.Id) },
            ["context"] = 0,
            ["data"] = new JsonObject
            {
                ["id"] = FakeUser.Snowflake(this.Rest.GetCommandId(commandName)),
                ["name"] = commandName.ToLowerInvariant(),
                ["type"] = 1,
                ["guild_id"] = FakeUser.Snowflake(this.Guild.Id),
                ["options"] = new JsonArray([.. options.Select(option => (JsonNode)option.ToJson())]),
                ["resolved"] = new JsonObject { ["users"] = resolvedUsers, ["members"] = resolvedMembers }
            }
        });

        return interaction;
    }
}

/// <summary>Identifies one interaction so a test can pick out the REST calls answering it.</summary>
public sealed record FakeInteraction(ulong Id, string Token)
{
    public bool IsCallback(RecordedRequest request)
        => request.Path == $"interactions/{this.Id}/{this.Token}/callback";

    public bool IsOriginalResponseEdit(RecordedRequest request)
        => request.Method == HttpMethod.Patch && request.Path.EndsWith($"/{this.Token}/messages/@original", StringComparison.Ordinal);
}

public sealed record SlashOption(string Name, int Type, JsonNode Value, FakeUser? ResolvedUser = null)
{
    public static SlashOption User(string name, FakeUser user) => new(name, 6, FakeUser.Snowflake(user.Id), user);

    public static SlashOption String(string name, string value) => new(name, 3, value);

    public JsonObject ToJson() => new() { ["name"] = this.Name, ["type"] = this.Type, ["value"] = this.Value.DeepClone() };
}
