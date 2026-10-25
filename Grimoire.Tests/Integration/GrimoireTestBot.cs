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
    /// <param name="seedLegacyRows">
    ///     Insert the obsolete <c>Guilds</c>, <c>Users</c> and <c>Members</c> rows a long-running production guild
    ///     already has. Pass <c>false</c> to see a guild the bot joined after those rows stopped being written.
    /// </param>
    public static async Task<GrimoireTestBot> StartAsync(GrimoireCoreFactory database, bool seedLegacyRows = true)
    {
        var guild = new FakeGuild();
        if (seedLegacyRows)
            await SeedLegacyRowsAsync(database, guild);

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
    ///     Workaround, remove once fixed: 36 foreign keys from the original schema still point at the obsolete
    ///     <c>Guilds</c>, <c>Members</c> and <c>Users</c> tables. The DomainRemodel migration dropped them, but it
    ///     was replaced by ModelRedesign in commit 16528ff without those drops, and the model snapshot no longer
    ///     knows about them. Nothing inserts these rows any more, so without them sins and user-log history fail.
    /// </summary>
    private static async Task SeedLegacyRowsAsync(GrimoireCoreFactory database, FakeGuild guild)
    {
        await using var dbContext = database.CreateDbContext();
        var guildId = (decimal)guild.Id;
        await dbContext.Database.ExecuteSqlAsync($"""INSERT INTO "Guilds" ("Id") VALUES ({guildId})""");
        foreach (var userId in guild.Members.Select(member => (decimal)member.User.Id))
        {
            await dbContext.Database.ExecuteSqlAsync($"""INSERT INTO "Users" ("Id") VALUES ({userId})""");
            await dbContext.Database.ExecuteSqlAsync(
                $"""INSERT INTO "Members" ("UserId", "GuildId") VALUES ({userId}, {guildId})""");
        }
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
