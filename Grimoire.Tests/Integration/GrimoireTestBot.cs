// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json.Nodes;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Net;
using DSharpPlus.Net.Gateway;
using Grimoire.Features.Leveling;
using Grimoire.Features.LogCleanup;
using Grimoire.Features.Moderation.Lock;
using Grimoire.Features.Moderation.Mute;
using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Gateway;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Grimoire.Utilities;
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
    /// <summary>Added to the second guild's ids. Users are shared between the guilds.</summary>
    public const ulong SecondGuildIdOffset = 1_000_000;

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
    private readonly Dictionary<Type, object> _backgroundServices = [];
    private readonly List<IHostedService> _startedServices = [];
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

    /// <summary>A second guild with its own channels and roles and the same users, when the test asked for one.</summary>
    public FakeGuild? SecondGuild { get; private init; }

    /// <summary>The guild a channel belongs to, so helpers given a second-guild channel act in that guild.</summary>
    public FakeGuild GuildOf(FakeChannel channel)
        => this.SecondGuild is { } second && (second.Channels.Contains(channel) || second.Threads.Contains(channel))
            ? second
            : this.Guild;
    public FakeGatewayTransport Gateway { get; }
    public FakeDiscordRest Rest { get; }
    public TestLogSink Logs { get; }

    /// <summary>Image downloads the bot made, for avatars and attachments in log embeds.</summary>
    public FakeCdn Cdn { get; private init; } = null!;
    public IServiceProvider Services => this._host.Services;

    public async ValueTask DisposeAsync()
    {
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var service in this._startedServices)
            await service.StopAsync(stopping.Token);
        await this._host.StopAsync(stopping.Token);
        this._host.Dispose();
    }

    /// <param name="database">The shared Postgres fixture. Reset it before starting a bot.</param>
    /// <param name="configureGuild">Changes the fake guild before the bot connects, such as narrower bot permissions.</param>
    /// <param name="unregisteredCommands">
    ///     Command classes that production forgot to register, added only for this test bot so their logic can be
    ///     tested. Each use should cite the known issue that tracks the missing registration.
    /// </param>
    public static async Task<GrimoireTestBot> StartAsync(GrimoireCoreFactory database,
        Action<FakeGuild>? configureGuild = null,
        IReadOnlyList<Type>? unregisteredCommands = null,
        Action<HostApplicationBuilder>? configureHost = null,
        bool secondGuild = false,
        string? connectionString = null)
    {
        var guild = new FakeGuild();
        configureGuild?.Invoke(guild);
        var second = secondGuild ? new FakeGuild(SecondGuildIdOffset) : null;

        var gateway = new FakeGatewayTransport(guild, second);
        var rest = new FakeDiscordRest(guild) { SecondGuild = second };
        var logs = new TestLogSink();

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["token"] = "fake.discord.token",
            // A test can point the bot at a database of its own, such as an empty one for the startup tests.
            ["ConnectionStrings:Grimoire"] = connectionString ?? database.ConnectionString,
            ["pluralkitApiEndpoint"] = "http://pluralkit.fake.invalid/",
            ["pluralkitUserAgent"] = "Grimoire.Tests",
            // Read by DiscordImageEmbedService, which the user-log handlers need. Same value as appsettings.json.
            ["validImageExtensions"] = ".jpg,.jpeg,.png,.webp,.gif"
        });

        builder.Services.AddGrimoire(builder.Configuration);
        // Extra configuration or replaced services for one test, such as alert channels or a manual clock.
        configureHost?.Invoke(builder);

        // The two seams: DSharpPlus resolves its websocket transport and its REST HttpClient from DI.
        builder.Services.Replace(ServiceDescriptor.Singleton<ITransportService>(gateway));
        builder.Services.AddHttpClient("DSharpPlus.Rest.HttpClient")
            .ConfigurePrimaryHttpMessageHandler(() => rest);
        // Image downloads for log embeds go to a fake CDN, so no test touches the real network.
        var cdn = new FakeCdn();
        builder.Services.AddHttpClient("Default").ConfigurePrimaryHttpMessageHandler(() => cdn);

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
        var bot = new GrimoireTestBot(host, guild, gateway, rest, logs) { Cdn = cdn, SecondGuild = second };
        rest.Dispatch = gateway.Dispatch;
        // Commands are built from the extension's list when the client starts, so these join the real ones.
        if (unregisteredCommands is { Count: > 0 })
            host.Services.GetRequiredService<CommandsExtension>().AddCommands(unregisteredCommands);

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
    public async Task ConfigureModerationAsync(FakeGuild? guild = null)
    {
        guild ??= this.Guild;
        var settings = this.Services.GetRequiredService<SettingsModule>();
        var guildId = new GuildId(guild.Id);
        var moderatorId = new ModeratorId(guild.Owner.Id);

        (await settings.SetModuleState(Module.Moderation, guildId, moderatorId, true)).ShouldBeSetOrUnchanged();
        (await settings.SetLogChannelSetting(GuildLogType.Moderation, guildId, moderatorId,
            new ChannelId(guild.ModLog.Id))).ShouldBeSetOrUnchanged();
    }

    public bool IsModLogPost(RecordedRequest request)
        => request.Method == HttpMethod.Post && request.Path == $"channels/{this.Guild.ModLog.Id}/messages";

    /// <summary>Waits for a mod-log post whose first embed matches, and returns that embed.</summary>
    public async Task<JsonNode> WaitForModLogEmbedAsync(Func<JsonNode, bool> match)
        => (await this.Rest.WaitForRequestAsync(request =>
                this.IsModLogPost(request) && request.Json?["embeds"]?[0] is { } embed && match(embed)))
            .Json!["embeds"]![0]!;

    /// <summary>Runs a slash command and waits for the reply the invoking member sees.</summary>
    public Task<FakeReply> RunSlashCommandAndWaitAsync(string commandName, FakeUser invoker, params SlashOption[] options)
        => FakeReply.WaitForAsync(this.Rest, this.RunSlashCommand(commandName, invoker, options));

    public Task<FakeReply> RunSlashCommandAndWaitAsync(FakeChannel channel, string commandName, FakeUser invoker,
        params SlashOption[] options)
        => FakeReply.WaitForAsync(this.Rest, this.RunSlashCommand(channel, commandName, invoker, options));

    /// <summary>
    ///     Runs one tick of a timer-driven background service now, the way its loop does: in a new scope, through its
    ///     protected <c>RunTask</c>. The same instance is reused, so retry backoff carries over between ticks.
    /// </summary>
    public async Task RunBackgroundTickAsync<T>() where T : GenericBackgroundService
    {
        var service = this.BackgroundService<T>();
        var runTask = typeof(T).GetMethod("RunTask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                      ?? throw new InvalidOperationException($"{typeof(T).Name} has no RunTask method.");
        await using var scope = this.Services.CreateAsyncScope();
        await (Task)runTask.Invoke(service, [scope.ServiceProvider, CancellationToken.None])!;
    }

    /// <summary>
    ///     Starts a background service that the test host leaves out, on its real timer. Its first tick comes after
    ///     a random delay of up to one period plus one period, so allow about twice its interval. It stops with the bot.
    /// </summary>
    public async Task StartBackgroundServiceAsync<T>() where T : GenericBackgroundService
    {
        var service = this.BackgroundService<T>();
        await service.StartAsync(CancellationToken.None);
        this._startedServices.Add(service);
    }

    private T BackgroundService<T>() where T : GenericBackgroundService
        => (T)(this._backgroundServices.TryGetValue(typeof(T), out var existing)
            ? existing
            : this._backgroundServices[typeof(T)] = ActivatorUtilities.CreateInstance<T>(this.Services));

    /// <summary>A moderator deletes a channel or thread, and the gateway tells the bot.</summary>
    public void DeleteChannel(FakeChannel channel)
    {
        var json = this.Guild.ChannelJson(channel);
        this.Guild.DeleteChannel(channel);
        this.Gateway.Dispatch(channel.IsThread ? "THREAD_DELETE" : "CHANNEL_DELETE", json);
    }

    /// <summary>The member leaves the guild, and the gateway tells the bot.</summary>
    public void MemberLeaves(FakeUser user)
    {
        this.Guild.Depart(user);
        this.Gateway.Dispatch("GUILD_MEMBER_REMOVE", new JsonObject
        {
            ["guild_id"] = FakeUser.Snowflake(this.Guild.Id),
            ["user"] = user.ToJson()
        });
    }

    /// <summary>The member joins again with no roles, and the gateway tells the bot.</summary>
    /// <summary>
    ///     A member joins with an invite. Its use count goes up, and a single-use invite is used up and disappears,
    ///     as on Discord.
    /// </summary>
    public void MemberJoinsWithInvite(FakeUser user, FakeInvite invite)
    {
        invite.Uses++;
        if (invite.MaxUses > 0 && invite.Uses >= invite.MaxUses)
            this.Guild.Invites.Remove(invite);
        this.MemberJoins(user);
    }

    public void MemberJoins(FakeUser user)
    {
        this.Guild.Return(user);
        var member = FakeGuild.MemberJson(this.Guild.GetMember(user));
        member["guild_id"] = FakeUser.Snowflake(this.Guild.Id);
        member["joined_at"] = DateTimeOffset.UtcNow.ToString("O");
        this.Gateway.Dispatch("GUILD_MEMBER_ADD", member);
    }

    /// <summary>
    ///     A member changes their nickname, username or avatar, and the gateway sends <c>GUILD_MEMBER_UPDATE</c>. Pass
    ///     only what changes. A nickname of "" clears it.
    /// </summary>
    public void UpdateProfile(FakeUser user, string? nickname = null, string? username = null, string? avatarHash = null)
    {
        var member = FakeGuild.MemberJson(this.Guild.GetMember(user));
        member["guild_id"] = FakeUser.Snowflake(this.Guild.Id);
        if (nickname is not null)
            member["nick"] = nickname.Length == 0 ? null : nickname;
        var userJson = member["user"]!.AsObject();
        if (username is not null)
        {
            userJson["username"] = username;
            userJson["global_name"] = username;
        }

        if (avatarHash is not null)
            userJson["avatar"] = avatarHash;
        this.Gateway.Dispatch("GUILD_MEMBER_UPDATE", member);
    }

    /// <summary>A moderator changes a member's roles by hand, and the gateway tells the bot.</summary>
    public void SetRolesByHand(FakeUser user, params ulong[] roleIds)
    {
        this.Guild.SetRoles(user, roleIds);
        var member = FakeGuild.MemberJson(this.Guild.GetMember(user));
        member["guild_id"] = FakeUser.Snowflake(this.Guild.Id);
        this.Gateway.Dispatch("GUILD_MEMBER_UPDATE", member);
    }

    /// <summary>A moderator bans someone from Discord's own client, not through the bot.</summary>
    public void BanFromClient(FakeUser moderator, FakeUser target, string? reason = null)
    {
        this.Rest.RememberUser(target);
        this.Rest.ApplyBan(moderator, target.Id, reason);
    }

    /// <summary>A moderator lifts a ban from Discord's own client.</summary>
    public void UnbanFromClient(FakeUser moderator, FakeUser target) => this.Rest.ApplyUnban(moderator, target.Id);

    /// <summary>Records a thread lock the way <c>/Lock</c> does, without going through the command.</summary>
    public async Task LockThreadAsync(FakeChannel thread, TimeSpan duration)
    {
        var now = DateTimeOffset.UtcNow;
        var threadLock = ThreadLocked.Create(new ModeratorId(this.Guild.Moderator.Id), null,
            new ChannelId(thread.Id), new GuildId(this.Guild.Id), now, now + duration).ShouldSucceed();
        (await this.Services.GetRequiredService<SettingsModule>().ApplyThreadLockAction(threadLock)).ShouldSucceed();
    }

    /// <summary>Records an unlock, as <c>/Unlock</c> or the lock background task does when the timer runs out.</summary>
    public async Task UnlockThreadAsync(FakeChannel thread)
    {
        var unlock = ThreadUnlocked.Create(new ModeratorId(this.Guild.Moderator.Id), new ChannelId(thread.Id),
            new GuildId(this.Guild.Id), DateTimeOffset.UtcNow).ShouldSucceed();
        (await this.Services.GetRequiredService<SettingsModule>().ApplyThreadLockAction(unlock)).ShouldSucceed();
    }

    public async Task SetModuleAsync(Module module, bool enabled, FakeGuild? guild = null)
        => (await this.Services.GetRequiredService<SettingsModule>().SetModuleState(module,
            new GuildId((guild ?? this.Guild).Id), new ModeratorId(this.Guild.Owner.Id), enabled)).ShouldBeSetOrUnchanged();

    /// <summary>Turns on the message log, which logs every channel unless an override says otherwise.</summary>
    public async Task ConfigureMessageLogAsync()
        => (await this.Services.GetRequiredService<SettingsModule>().SetModuleState(Module.MessageLog,
            new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id), true)).ShouldBeSetOrUnchanged();

    /// <summary>
    ///     Sends a <c>MESSAGE_CREATE</c> and returns the message id. Ids are minted from the current time because the
    ///     bot dates a message by its snowflake, and the log cleanup purges messages by that date.
    /// </summary>
    public ulong SendMessage(FakeUser author, FakeChannel channel, string content)
    {
        var messageId = this.NextSnowflake();
        this.Gateway.Dispatch("MESSAGE_CREATE", this.MessageJson(messageId, author, channel, content, edited: false));
        return messageId;
    }

    /// <summary>The author edits a message, and the gateway sends <c>MESSAGE_UPDATE</c> with the new content.</summary>
    public void EditMessage(ulong messageId, FakeUser author, FakeChannel channel, string newContent)
        => this.Gateway.Dispatch("MESSAGE_UPDATE", this.MessageJson(messageId, author, channel, newContent, edited: true));

    /// <summary>A message is deleted, and the gateway sends <c>MESSAGE_DELETE</c>.</summary>
    public void DeleteMessage(ulong messageId, FakeChannel channel)
        => this.Gateway.Dispatch("MESSAGE_DELETE", new JsonObject
        {
            ["id"] = FakeUser.Snowflake(messageId),
            ["channel_id"] = FakeUser.Snowflake(channel.Id),
            ["guild_id"] = FakeUser.Snowflake(this.GuildOf(channel).Id)
        });

    /// <summary>Several messages are deleted at once, and the gateway sends <c>MESSAGE_DELETE_BULK</c>.</summary>
    public void BulkDeleteMessages(FakeChannel channel, params ulong[] messageIds)
        => this.Gateway.Dispatch("MESSAGE_DELETE_BULK", new JsonObject
        {
            ["ids"] = new JsonArray([.. messageIds.Select(id => (JsonNode)FakeUser.Snowflake(id))]),
            ["channel_id"] = FakeUser.Snowflake(channel.Id),
            ["guild_id"] = FakeUser.Snowflake(this.GuildOf(channel).Id)
        });

    private JsonObject MessageJson(ulong messageId, FakeUser author, FakeChannel channel, string content, bool edited)
    {
        var guild = this.GuildOf(channel);
        return new JsonObject
        {
            ["id"] = FakeUser.Snowflake(messageId),
            ["channel_id"] = FakeUser.Snowflake(channel.Id),
            ["guild_id"] = FakeUser.Snowflake(guild.Id),
            ["author"] = author.ToJson(),
            // Webhook authors, such as PluralKit's proxies, aren't members, so Discord sends no member for them.
            ["member"] = guild.Members.Any(member => member.User.Id == author.Id)
            ? FakeGuild.MemberJson(guild.GetMember(author), false)
            : null,
            ["webhook_id"] = guild.Members.Any(member => member.User.Id == author.Id) ? null : FakeUser.Snowflake(author.Id),
            ["content"] = content,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            ["edited_timestamp"] = edited ? DateTimeOffset.UtcNow.ToString("O") : null,
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
        };
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
        => this.RunSlashCommand(this.Guild.General, commandName, invoker, options);

    /// <summary>Runs a slash command from <paramref name="channel" />, which is what "Current Channel" options use.</summary>
    public FakeInteraction RunSlashCommand(FakeChannel channel, string commandName, FakeUser invoker,
        params SlashOption[] options)
    {
        var guild = this.GuildOf(channel);
        var number = Interlocked.Increment(ref this._interactionCount);
        var interaction = new FakeInteraction(1_400_000_000_000_000_000UL + (ulong)number, $"fake-interaction-token-{number}");
        var member = guild.GetMember(invoker);
        var memberJson = FakeGuild.MemberJson(member);
        memberJson["permissions"] = guild.GuildPermissionsOf(invoker).ToString(CultureInfo.InvariantCulture);

        var resolvedUsers = new JsonObject();
        var resolvedMembers = new JsonObject();
        foreach (var user in options.SelectMany(option => option.ResolvedUsers()).Distinct())
        {
            this.Rest.RememberUser(user);
            resolvedUsers[FakeUser.Snowflake(user.Id)] = user.ToJson();
            if (guild.Members.FirstOrDefault(m => m.User.Id == user.Id) is { } resolvedMember)
                resolvedMembers[FakeUser.Snowflake(user.Id)] = FakeGuild.MemberJson(resolvedMember, false);
        }

        var resolvedRoles = new JsonObject();
        foreach (var roleId in options.SelectMany(option => option.ResolvedRoles()).Distinct())
            if (guild.RoleJsonOf(roleId) is { } role)
                resolvedRoles[FakeUser.Snowflake(roleId)] = role;

        // DSharpPlus reads resolved channels without a null check, so the collection is always sent.
        var resolvedChannels = new JsonObject();
        foreach (var resolvedChannel in options.SelectMany(option => option.ResolvedChannels()).Distinct())
            resolvedChannels[FakeUser.Snowflake(resolvedChannel.Id)] = guild.ChannelJson(resolvedChannel);

        this.Gateway.Dispatch("INTERACTION_CREATE", new JsonObject
        {
            ["id"] = FakeUser.Snowflake(interaction.Id),
            ["application_id"] = FakeUser.Snowflake(guild.ApplicationId),
            ["type"] = 2,
            ["token"] = interaction.Token,
            ["version"] = 1,
            ["guild_id"] = FakeUser.Snowflake(guild.Id),
            ["channel_id"] = FakeUser.Snowflake(channel.Id),
            ["channel"] = guild.ChannelJson(channel),
            ["member"] = memberJson,
            ["app_permissions"] = guild.BotPermissions.ToString(CultureInfo.InvariantCulture),
            ["locale"] = "en-US",
            ["guild_locale"] = "en-US",
            ["entitlements"] = new JsonArray(),
            ["authorizing_integration_owners"] = new JsonObject { ["0"] = FakeUser.Snowflake(guild.Id) },
            ["context"] = 0,
            ["data"] = new JsonObject
            {
                ["id"] = FakeUser.Snowflake(this.Rest.GetCommandId(commandName)),
                ["name"] = commandName.ToLowerInvariant(),
                ["type"] = 1,
                ["guild_id"] = FakeUser.Snowflake(guild.Id),
                ["options"] = new JsonArray([.. options.Select(option => (JsonNode)option.ToJson())]),
                ["resolved"] = new JsonObject
                {
                    ["users"] = resolvedUsers,
                    ["members"] = resolvedMembers,
                    ["channels"] = resolvedChannels,
                    ["roles"] = resolvedRoles
                }
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

    /// <summary>The bot deleted its deferred response, which leaves the invoker with nothing.</summary>
    public bool IsOriginalResponseDelete(RecordedRequest request)
        => request.Method == HttpMethod.Delete && request.Path.EndsWith($"/{this.Token}/messages/@original", StringComparison.Ordinal);
}

/// <summary>
///     What the member who ran a command sees: the bot's first reply, whether it answered straight away or deferred
///     first and edited the original response later.
/// </summary>
public sealed record FakeReply(JsonNode Message, bool IsEphemeral, bool WasDeferred)
{
    private const int EphemeralFlag = 1 << 6;
    private const int DeferredCallbackType = 5;

    public JsonNode? Embed => this.Message["embeds"]?.AsArray().FirstOrDefault();

    /// <summary>The embed's description, which is where <c>ReplyAsync</c> and the error handler put their text.</summary>
    public string? Description => this.Embed?["description"]?.GetValue<string>();

    public static async Task<FakeReply> WaitForAsync(FakeDiscordRest rest, FakeInteraction interaction)
    {
        var callback = (await rest.WaitForRequestAsync(interaction.IsCallback)).Json!;
        var data = callback["data"];
        var isEphemeral = ((data?["flags"]?.GetValue<int>() ?? 0) & EphemeralFlag) != 0;
        if (callback["type"]!.GetValue<int>() != DeferredCallbackType)
            return new FakeReply(data!, isEphemeral, false);

        var edit = await rest.WaitForRequestAsync(interaction.IsOriginalResponseEdit);
        return new FakeReply(edit.Json!, isEphemeral, true);
    }
}

/// <summary>
///     A slash command option as Discord sends it. Names are the registered (snake_case) names, such as
///     <c>mod_log_channel</c>. Enum options are integers holding the enum's numeric value.
/// </summary>
public sealed record SlashOption(
    string Name,
    int Type,
    JsonNode? Value,
    FakeUser? ResolvedUser = null,
    IReadOnlyList<SlashOption>? Options = null,
    FakeChannel? ResolvedChannel = null,
    ulong? ResolvedRoleId = null)
{
    private const int SubcommandType = 1;
    private const int SubcommandGroupType = 2;

    public static SlashOption User(string name, FakeUser user) => new(name, 6, FakeUser.Snowflake(user.Id), user);

    public static SlashOption String(string name, string value) => new(name, 3, value);

    public static SlashOption Integer(string name, long value) => new(name, 4, value);

    public static SlashOption Enum<TEnum>(string name, TEnum value) where TEnum : struct, Enum
        => Integer(name, Convert.ToInt64(value, CultureInfo.InvariantCulture));

    public static SlashOption Boolean(string name, bool value) => new(name, 5, value);

    public static SlashOption Channel(string name, FakeChannel channel)
        => new(name, 7, FakeUser.Snowflake(channel.Id), ResolvedChannel: channel);

    public static SlashOption Role(string name, ulong roleId) => new(name, 8, FakeUser.Snowflake(roleId), ResolvedRoleId: roleId);

    /// <summary>A mentionable option (type 9) holding a member.</summary>
    public static SlashOption MentionableUser(string name, FakeUser user) => new(name, 9, FakeUser.Snowflake(user.Id), user);

    /// <summary>A mentionable option (type 9) holding a role.</summary>
    public static SlashOption MentionableRole(string name, ulong roleId)
        => new(name, 9, FakeUser.Snowflake(roleId), ResolvedRoleId: roleId);

    public static SlashOption Subcommand(string name, params SlashOption[] options)
        => new(name, SubcommandType, null, Options: options);

    public static SlashOption Group(string name, params SlashOption[] subcommands)
        => new(name, SubcommandGroupType, null, Options: subcommands);

    /// <summary>Users anywhere in this option tree, which Discord lists in the interaction's resolved data.</summary>
    public IEnumerable<FakeUser> ResolvedUsers()
        => (this.ResolvedUser is { } user ? [user] : Array.Empty<FakeUser>())
            .Concat((this.Options ?? []).SelectMany(option => option.ResolvedUsers()));

    /// <summary>Roles anywhere in this option tree, which Discord lists in the interaction's resolved data.</summary>
    public IEnumerable<ulong> ResolvedRoles()
        => (this.ResolvedRoleId is { } roleId ? [roleId] : Array.Empty<ulong>())
            .Concat((this.Options ?? []).SelectMany(option => option.ResolvedRoles()));

    /// <summary>Channels anywhere in this option tree, which Discord lists in the interaction's resolved data.</summary>
    public IEnumerable<FakeChannel> ResolvedChannels()
        => (this.ResolvedChannel is { } channel ? [channel] : Array.Empty<FakeChannel>())
            .Concat((this.Options ?? []).SelectMany(option => option.ResolvedChannels()));

    public JsonObject ToJson()
    {
        var json = new JsonObject { ["name"] = this.Name, ["type"] = this.Type };
        if (this.Options is not null)
            json["options"] = new JsonArray([.. this.Options.Select(option => (JsonNode)option.ToJson())]);
        else
            json["value"] = this.Value?.DeepClone();
        return json;
    }
}
