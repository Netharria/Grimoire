// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.IntegrationTests.Database.SettingsLayer;
using Grimoire.Settings.Domain.Documents;
using Grimoire.Settings.Domain.Values;
using LanguageExt;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Grimoire.IntegrationTests.Database.Migrations;

/// <summary>
///     Spec 2.2, sections 2.5 and 8: Settings <c>Initial</c> copies main's legacy settings into the documents and
///     state rows. Each test seeds main's tables on a database migrated to the step before <c>Initial</c>, runs the
///     rest of the chain as the first deploy will, and reads the result back through the settings module, so it pins
///     what the bot sees, not only the rows.
/// </summary>
[Collection("Test collection")]
public sealed class SettingsCopyTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const long Configured = 1;
    private const long Untouched = 2;
    private const long Moderator = 9;
    private const long MuteRole = 17;

    /// <summary>Above 2^53, where a JavaScript reader would round it: copied exactly (OQ-3).</summary>
    private const long LargeChannel = 1_234_567_890_123_456_789;

    private static readonly DateTimeOffset _sinOn = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly string _name = $"copy_{Guid.NewGuid():N}";

    private string ConnectionString
        => new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = this._name }.ToString();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await ExecuteAsync(database.ConnectionString, $"CREATE DATABASE {this._name}");
        await using var chain = MigrationChain.Context(this.ConnectionString);
        var migrations = chain.Database.GetMigrations().ToList();
        var initial = migrations.FindIndex(name => name.EndsWith("_Initial", StringComparison.Ordinal));
        await chain.GetService<IMigrator>().MigrateAsync(migrations[initial - 1], cancellationToken: Token);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecuteAsync(database.ConnectionString, $"DROP DATABASE IF EXISTS {this._name} WITH (FORCE)");
    }

    [Fact]
    public async Task EveryLegacySetting_IsReadTheSameWayAfterTheCopy()
    {
        await this.SeedSettingsAsync();
        await this.FinishTheChainAsync();
        var settings = SettingsModuleFactory.Create(this.ConnectionString);
        var guild = new GuildId(Configured);

        var modules = await settings.GetAllModuleState(guild, Token).ShouldSucceed();
        modules.ShouldBe(new ModuleStates { Leveling = true, Moderation = true, AntiSpam = true });

        (await settings.GetLogChannelSetting(GuildLogType.Moderation, guild, Token)).ShouldSucceed()
            .ShouldBe(LanguageExt.Prelude.Some(new ChannelId(10)));
        (await settings.GetUserCommandChannel(guild, Token)).ShouldSucceed().ShouldBe(LanguageExt.Prelude.Some(new ChannelId(11)));

        var leveling = await settings.GetLevelingSettings(guild, Token).ShouldSucceed();
        leveling.XpTimeoutPeriod.Value.ShouldBe(TimeSpan.FromMinutes(10));
        leveling.Base.Value.ShouldBe(20);
        leveling.Modifier.Value.ShouldBe(60);
        leveling.Amount.Value.ShouldBe(7);

        long[] channels = [12, 13, 14, 15, 16, 18, 19, 20, 21, 22];
        GuildLogType[] types =
        [
            GuildLogType.Leveling, GuildLogType.MessageDeleted, GuildLogType.BulkMessageDeleted,
            GuildLogType.MessageEdited, GuildLogType.PublicModeration, GuildLogType.UserJoined, GuildLogType.UserLeft,
            GuildLogType.UsernameUpdated, GuildLogType.NicknameUpdated, GuildLogType.AvatarUpdated
        ];
        foreach (var (type, channel) in types.Zip(channels))
            (await settings.GetLogChannelSetting(type, guild, Token)).ShouldSucceed()
                .ShouldBe(LanguageExt.Prelude.Some(new ChannelId((ulong)channel)), type.ToString());

        (await settings.GetAutoPardonDuration(guild, Token)).ShouldSucceed().ShouldBe(TimeSpan.FromDays(30));
        (await settings.GetConfiguredMuteRole(guild, Token)).ShouldSucceed().ShouldBe(LanguageExt.Prelude.Some(new RoleId(MuteRole)));
    }

    [Fact]
    public async Task AGuildWithOnlyDefaults_GetsNoDocuments_AndReadsAsDefaults()
    {
        await this.SeedSettingsAsync();
        await this.FinishTheChainAsync();
        var settings = SettingsModuleFactory.Create(this.ConnectionString);
        var guild = new GuildId(Untouched);

        await using (var dbContext = this.SettingsContext())
            (await dbContext.GuildSettings.AnyAsync(row => row.GuildId == guild, Token)).ShouldBeFalse();

        (await settings.GetAllModuleState(guild, Token)).ShouldSucceed().ShouldBe(ModuleStates.Empty);
        (await settings.GetLevelingSettings(guild, Token)).ShouldSucceed().ShouldBe(LevelingConfig.Empty.Resolve());
        (await settings.GetAutoPardonDuration(guild, Token)).ShouldSucceed()
            .ShouldBe(SettingsModule.DefaultAutoPardonDuration);
        (await settings.GetLogChannelSetting(GuildLogType.UserJoined, guild, Token)).ShouldSucceed()
            .ShouldBe(Option<ChannelId>.None);
    }

    [Fact]
    public async Task OnlyTheChosenValues_AreStored_AsVersionOne()
    {
        await this.SeedSettingsAsync();
        await this.FinishTheChainAsync();
        await using var dbContext = this.SettingsContext();

        var leveling = await SettingsRows.LatestDocument(dbContext, new GuildId(Configured), SettingsDocument.Leveling,
            Token);
        leveling.ShouldNotBeNull().Version.ShouldBe(1);
        leveling.SetBy.ShouldBe(new ModeratorId(0));
        SettingsJson.Parse<LevelingConfig>(leveling.Value).ShouldBe(new LevelingConfig
        {
            XpTimeout = XpTimeoutPeriod.FromDatabase(600),
            LevelScalingBase = LevelScalingBase.FromDatabase(20),
            LevelScalingModifier = LevelScalingModifier.FromDatabase(60),
            XpGainAmount = XpGainAmount.FromDatabase(7),
            LogChannel = new ChannelId(12)
        });
        var general = await SettingsRows.LatestDocument(dbContext, new GuildId(3), SettingsDocument.General, Token);
        general.ShouldNotBeNull().Value.ShouldContain($"\"{LargeChannel}\"");
        SettingsJson.Parse<GeneralConfig>(general.Value).ModerationLogChannel
            .ShouldBe(new ChannelId(LargeChannel));
    }

    /// <summary>OQ-2: every legacy mute is Active, in force or overdue, with its sin's moderator and time.</summary>
    [Fact]
    public async Task EveryLegacyMute_IsActive_WithItsSinsModeratorAndTime_AndTheMuteRole()
    {
        await this.SeedSettingsAsync();
        await this.SeedAsync($"""
            INSERT INTO public."Sins" ("Id", "UserId", "ModeratorId", "GuildId", "SinOn", "SinType")
            OVERRIDING SYSTEM VALUE VALUES
                (1, 100, {Moderator}, {Configured}, '{_sinOn:O}', 1),
                (2, 101, {Moderator}, {Configured}, '{_sinOn:O}', 1);
            INSERT INTO public."Mutes" ("SinId", "UserId", "GuildId", "EndTime") VALUES
                (1, 100, {Configured}, now() + interval '1 hour'),
                (2, 101, {Configured}, now() - interval '1 day'),
                (99, 103, {Untouched}, now() + interval '1 hour');
            """);
        await this.FinishTheChainAsync();
        var settings = SettingsModuleFactory.Create(this.ConnectionString);

        var inForce = await settings.GetActiveMute(new UserId(100), new GuildId(Configured), Token).ShouldSucceed();
        inForce.State.ShouldBe(new ItemState.Active(_sinOn));
        inForce.ModeratorId.ShouldBe(new ModeratorId(Moderator));
        inForce.SetAt.ShouldBe(_sinOn);
        inForce.MuteRoleId.ShouldBe(new RoleId(MuteRole));

        var overdue = await settings.GetAllExpiredMutes(Token).ToListAsync(Token);
        overdue.ShouldHaveSingleItem().UserId.ShouldBe(new UserId(101));

        // A mute whose sin is missing gets the sentinels, and a guild with no mute role none.
        var orphan = await settings.GetActiveMute(new UserId(103), new GuildId(Untouched), Token).ShouldSucceed();
        orphan.ModeratorId.ShouldBe(new ModeratorId(0));
        orphan.SetAt.ShouldBe(DateTimeOffset.MinValue);
        orphan.MuteRoleId.ShouldBeNull();
    }

    [Fact]
    public async Task ALegacyLock_IsActive_WithItsPermissions()
    {
        await this.SeedSettingsAsync();
        await this.SeedAsync($"""
            INSERT INTO public."Locks" ("ChannelId", "GuildId", "ModeratorId", "Reason", "EndTime",
                                        "PreviouslyAllowed", "PreviouslyDenied") VALUES
                (30, {Configured}, NULL, '', now() + interval '1 hour', 2048, 64);
            """);
        await this.FinishTheChainAsync();
        var settings = SettingsModuleFactory.Create(this.ConnectionString);

        var channelLock = await settings.GetActiveChannelLockAsync(new ChannelId(30), new GuildId(Configured), Token)
            .ShouldSucceed();
        channelLock.InForce.ShouldBeTrue();
        channelLock.Reason.ShouldBeNull();
        channelLock.ModeratorId.ShouldBe(new ModeratorId(0));
        channelLock.PreviouslyAllowed.Permissions.ShouldBe(2048);
        channelLock.PreviouslyDenied.Permissions.ShouldBe(64);
    }

    /// <summary>
    ///     Spec 2.5, section 6.3: main's collection tables become one version-1 document per guild and collection,
    ///     keeping only what is in force. A reward at level 0 becomes level 1 (OQ-3), an override that meant "inherit"
    ///     is left out, and a guild with nothing in a collection gets no document.
    /// </summary>
    [Fact]
    public async Task EveryLegacyCollection_BecomesADocument_ReadTheSameWay()
    {
        await this.SeedSettingsAsync();
        await this.SeedAsync($"""
            INSERT INTO public."Rewards" ("RoleId", "GuildId", "RewardLevel", "RewardMessage") VALUES
                (40, {Configured}, 5, 'Well done'), (41, {Configured}, 0, NULL), ({LargeChannel}, 3, 10, NULL);
            INSERT INTO public."IgnoredChannels" ("ChannelId", "GuildId") VALUES (50, {Configured}), (9, {Configured});
            INSERT INTO public."IgnoredMembers" ("UserId", "GuildId") VALUES (51, {Configured});
            INSERT INTO public."IgnoredRoles" ("RoleId", "GuildId") VALUES (52, {Configured});
            INSERT INTO public."MessagesLogChannelOverrides" ("ChannelId", "GuildId", "ChannelOption") VALUES
                (60, {Configured}, 0), (61, {Configured}, 1), (62, {Configured}, 2);
            INSERT INTO public."SpamFilterOverrides" ("ChannelId", "GuildId", "ChannelOption") VALUES
                (70, {Configured}, 0), (71, {Configured}, 1), (72, {Configured}, 2), (73, 3, 2);
            """);
        await this.FinishTheChainAsync();
        var settings = SettingsModuleFactory.Create(this.ConnectionString);
        var guild = new GuildId(Configured);

        (await settings.GetLevelingRewardsAsync(guild, Token)).ShouldSucceed().ShouldBe([
            new RewardEntry(new RoleId(40), 5, RewardMessage.FromDatabase("Well done")),
            new RewardEntry(new RoleId(41), 1, null)
        ], ignoreOrder: true);
        (await settings.GetLevelingRewardsAsync(new GuildId(3), Token)).ShouldSucceed()
            .ShouldBe([new RewardEntry(new RoleId(LargeChannel), 10, null)]);

        (await settings.GetAllIgnoredItems(guild, Token)).ShouldSucceed().ShouldBe([
            new XpTarget.Channel(new ChannelId(9)), new XpTarget.Channel(new ChannelId(50)),
            new XpTarget.Member(new UserId(51)), new XpTarget.Role(new RoleId(52))
        ], ignoreOrder: true);

        (await settings.GetAllOverriddenChannels(guild, Token)).ShouldSucceed().ShouldBe(
            LanguageExt.Prelude.toMap([
                (new ChannelId(60), MessageLogOverrideOption.AlwaysLog),
                (new ChannelId(61), MessageLogOverrideOption.NeverLog)
            ]));
        (await settings.GetAllSpamFilterOverrideAsync(guild, Token)).ShouldSucceed().ShouldBe(
            LanguageExt.Prelude.toMap([
                (new ChannelId(70), SpamFilterOverrideOption.AlwaysFilter),
                (new ChannelId(71), SpamFilterOverrideOption.NeverFilter)
            ]));

        await using var dbContext = this.SettingsContext();
        // Guild 3's only override meant "inherit", and guild 2 has nothing: no documents for them.
        (await SettingsRows.LatestDocument(dbContext, new GuildId(3), SettingsDocument.SpamFilterOverrides, Token))
            .ShouldBeNull();
        (await dbContext.GuildSettings.AnyAsync(row => row.GuildId == new GuildId(Untouched), Token)).ShouldBeFalse();
    }

    /// <summary>
    ///     A copied document is exactly what C# writes for the same content (spec 2.5, section 3.7), so an unchanged
    ///     write after the deploy is seen as unchanged, and the equivalence check's EXCEPT is exact.
    /// </summary>
    [Fact]
    public async Task ACopiedCollection_IsWhatTheBotWouldWrite()
    {
        await this.SeedSettingsAsync();
        await this.SeedAsync($"""
            INSERT INTO public."Rewards" ("RoleId", "GuildId", "RewardLevel", "RewardMessage") VALUES
                (40, {Configured}, 5, 'Well done'), (41, {Configured}, 3, NULL);
            INSERT INTO public."IgnoredChannels" ("ChannelId", "GuildId") VALUES (50, {Configured}), (9, {Configured});
            INSERT INTO public."IgnoredRoles" ("RoleId", "GuildId") VALUES (52, {Configured});
            """);
        await this.FinishTheChainAsync();
        await using var dbContext = this.SettingsContext();

        var rewards = await SettingsRows.LatestDocument(dbContext, new GuildId(Configured), SettingsDocument.Rewards,
            Token);
        rewards.ShouldNotBeNull().Version.ShouldBe(1);
        rewards.SetBy.ShouldBe(new ModeratorId(0));
        SettingsJson.Parse<LevelRewards>(rewards.Value).ShouldBe(LevelRewards.Empty
            .Set(new RewardEntry(new RoleId(41), 3, null)).Document
            .Set(new RewardEntry(new RoleId(40), 5, RewardMessage.FromDatabase("Well done"))).Document);
        var exclusions = await SettingsRows.LatestDocument(dbContext, new GuildId(Configured),
            SettingsDocument.XpExclusions, Token);
        var written = XpExclusions.Empty.Add([
            new XpTarget.Role(new RoleId(52)), new XpTarget.Channel(new ChannelId(50)),
            new XpTarget.Channel(new ChannelId(9))
        ]).Document;
        SettingsJson.Parse<XpExclusions>(exclusions.ShouldNotBeNull().Value).ShouldBe(written);

        // The same text as C# writes, once both go through jsonb's normal form.
        var normalised = await dbContext.Database
            .SqlQuery<string>($"SELECT CAST(CAST({SettingsJson.Serialize(written)} AS jsonb) AS text) AS \"Value\"")
            .SingleAsync(Token);
        exclusions.Value.ShouldBe(normalised);
    }

    /// <summary>
    ///     Guild 1 changed everything, guild 2 nothing, guild 3 only its moderation log, to a channel id above 2^53.
    ///     Each settings table gets a row per guild, as main created them.
    /// </summary>
    private Task SeedSettingsAsync()
        => this.SeedAsync($"""
            INSERT INTO public."Guilds" ("Id", "ModChannelLog", "UserCommandChannelId") VALUES
                ({Configured}, 10, 11), ({Untouched}, NULL, NULL), (3, {LargeChannel}, NULL);
            INSERT INTO public."GuildLevelSettings"
                ("GuildId", "ModuleEnabled", "TextTime", "Base", "Modifier", "Amount", "LevelChannelLogId") VALUES
                ({Configured}, true, interval '10 minutes', 20, 60, 7, 12),
                ({Untouched}, false, interval '3 minutes', 15, 50, 5, NULL),
                (3, false, interval '3 minutes', 15, 50, 5, NULL);
            INSERT INTO public."GuildMessageLogSettings"
                ("GuildId", "ModuleEnabled", "DeleteChannelLogId", "BulkDeleteChannelLogId", "EditChannelLogId") VALUES
                ({Configured}, false, 13, 14, 15), ({Untouched}, false, NULL, NULL, NULL), (3, false, NULL, NULL, NULL);
            INSERT INTO public."GuildModerationSettings"
                ("GuildId", "ModuleEnabled", "AntiSpamEnabled", "PublicBanLog", "AutoPardonAfter", "MuteRole") VALUES
                ({Configured}, true, true, 16, interval '30 days', {MuteRole}),
                ({Untouched}, false, false, NULL, interval '10950 days', NULL),
                (3, false, false, NULL, interval '10950 days', NULL);
            INSERT INTO public."GuildUserLogSettings"
                ("GuildId", "ModuleEnabled", "JoinChannelLogId", "LeaveChannelLogId", "UsernameChannelLogId",
                 "NicknameChannelLogId", "AvatarChannelLogId") VALUES
                ({Configured}, false, 18, 19, 20, 21, 22),
                ({Untouched}, false, NULL, NULL, NULL, NULL, NULL),
                (3, false, NULL, NULL, NULL, NULL, NULL);
            INSERT INTO public."GuildCommandsSettings" ("GuildId", "ModuleEnabled") VALUES
                ({Configured}, false), ({Untouched}, false), (3, false);
            """);

    /// <summary>Seeds main's tables with their foreign keys off, so a test seeds only the rows it is about.</summary>
    private Task SeedAsync(string sql)
        => ExecuteAsync(this.ConnectionString, $"SET session_replication_role = replica;\n{sql}");

    private async Task FinishTheChainAsync()
    {
        await using var chain = MigrationChain.Context(this.ConnectionString);
        await chain.Database.MigrateAsync(Token);
    }

    private SettingsDbContext SettingsContext()
        => new(new DbContextOptionsBuilder<SettingsDbContext>().UseNpgsql(this.ConnectionString).Options);

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
