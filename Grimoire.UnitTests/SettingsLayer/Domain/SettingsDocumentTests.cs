// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Grimoire.Settings.Domain.Documents;
using Grimoire.Settings.Domain.Values;
using LanguageExt;

namespace Grimoire.UnitTests.SettingsLayer.Domain;

/// <summary>Spec 2.2, section 8: the settings documents, their JSON, and what a missing field means.</summary>
public sealed class SettingsDocumentTests
{
    private static readonly ChannelId _largest = new(long.MaxValue);

    // ── JSON ──────────────────────────────────────────────────────────────────

    [Fact]
    public void EachDocument_RoundTripsThroughItsJson()
    {
        RoundTrip(new ModuleStates { Leveling = true, AntiSpam = true });
        RoundTrip(new GeneralConfig { ModerationLogChannel = new ChannelId(1), UserCommandChannel = new ChannelId(2) });
        RoundTrip(new LevelingConfig
        {
            XpGainAmount = XpGainAmount.FromDatabase(10),
            LevelScalingBase = LevelScalingBase.FromDatabase(20),
            LevelScalingModifier = LevelScalingModifier.FromDatabase(30),
            XpTimeout = XpTimeoutPeriod.FromDatabase(600),
            LogChannel = new ChannelId(3)
        });
        RoundTrip(new MessageLogConfig
        {
            DeleteLogChannel = new ChannelId(4), BulkDeleteLogChannel = new ChannelId(5), EditLogChannel = new ChannelId(6)
        });
        RoundTrip(new ModerationConfig
        {
            PublicModerationLogChannel = new ChannelId(7), AutoPardonAfter = TimeSpan.FromDays(30),
            MuteRole = new RoleId(8)
        });
        RoundTrip(new UserLogConfig
        {
            JoinLogChannel = new ChannelId(9), LeaveLogChannel = new ChannelId(10),
            UsernameLogChannel = new ChannelId(11), NicknameLogChannel = new ChannelId(12),
            AvatarLogChannel = new ChannelId(13)
        });
    }

    [Fact]
    public void AnEmptyObject_ReadsAsAllDefaults()
    {
        SettingsJson.Parse<ModuleStates>("{}").ShouldBe(ModuleStates.Empty);
        SettingsJson.Parse<LevelingConfig>("{}").ShouldBe(LevelingConfig.Empty);
        SettingsJson.Parse<ModerationConfig>("{}").ShouldBe(ModerationConfig.Empty);
    }

    [Fact]
    public void TheDefaults_AreLeftOut()
    {
        SettingsJson.Serialize(ModuleStates.Empty).ShouldBe("{}");
        SettingsJson.Serialize(new ModuleStates { Leveling = true }).ShouldBe("""{"Leveling":true}""");
        SettingsJson.Serialize(LevelingConfig.Empty).ShouldBe("{}");
    }

    [Fact]
    public void AnUnknownField_IsIgnored()
        => SettingsJson.Parse<ModuleStates>("""{"Leveling":true,"SomethingNew":42}""")
            .ShouldBe(new ModuleStates { Leveling = true });

    /// <summary>OQ-3: a JavaScript reader rounds numbers above 2^53, so ids are strings, and exact at 2^63 - 1.</summary>
    [Fact]
    public void ASnowflake_IsAStringAndExact()
    {
        var json = SettingsJson.Serialize(new GeneralConfig { UserCommandChannel = _largest });

        json.ShouldBe("""{"UserCommandChannel":"9223372036854775807"}""");
        SettingsJson.Parse<GeneralConfig>(json).UserCommandChannel.ShouldBe(_largest);
    }

    [Fact]
    public void Durations_AreWholeSeconds()
    {
        SettingsJson.Serialize(new ModerationConfig { AutoPardonAfter = TimeSpan.FromDays(1) })
            .ShouldBe("""{"AutoPardonAfterSeconds":86400}""");
        SettingsJson.Serialize(new LevelingConfig { XpTimeout = XpTimeoutPeriod.FromDatabase(180) })
            .ShouldBe("""{"XpTimeoutSeconds":180}""");
    }

    /// <summary>Reading a corrupt document as defaults would quietly turn a guild's modules and logs off.</summary>
    [Fact]
    public void ADocumentThatDoesNotParse_Throws()
        => Should.Throw<JsonException>(() => SettingsJson.Parse<ModuleStates>("""{"Leveling":"yes"}"""));

    // ── ModuleStates ──────────────────────────────────────────────────────────

    [Fact]
    public void ModuleStates_TurnsEachModuleOnAndOff()
    {
        foreach (var module in Enum.GetValues<Module>().Where(module => module != Module.General))
        {
            var on = ModuleStates.Empty.With(module, true).ShouldSucceed();

            ModuleStates.Empty.IsEnabled(module).ShouldBeFalse();
            on.IsEnabled(module).ShouldBeTrue();
            Enum.GetValues<Module>().Where(other => other != module && other != Module.General)
                .ShouldAllBe(other => !on.IsEnabled(other));
            on.With(module, false).ShouldSucceed().ShouldBe(ModuleStates.Empty);
        }
    }

    [Fact]
    public void ModuleStates_GeneralIsAlwaysOn_AndCannotBeTurnedOff()
    {
        ModuleStates.Empty.IsEnabled(Module.General).ShouldBeTrue();
        ModuleStates.Empty.With(Module.General, false).ShouldFail().ShouldBeOfType<Invalid>()
            .Message.ShouldBe("Cannot disable the general module.");
    }

    // ── Log channels ──────────────────────────────────────────────────────────

    [Fact]
    public void EachLogType_IsReadAndWrittenInItsDocument()
    {
        foreach (var type in Enum.GetValues<GuildLogType>())
        {
            var channel = new ChannelId(1000 + (ulong)type);
            var stored = type.Document() switch
            {
                SettingsDocument.General => Check<GeneralConfig>(type, channel),
                SettingsDocument.Leveling => Check<LevelingConfig>(type, channel),
                SettingsDocument.MessageLog => Check<MessageLogConfig>(type, channel),
                SettingsDocument.Moderation => Check<ModerationConfig>(type, channel),
                SettingsDocument.UserLog => Check<UserLogConfig>(type, channel),
                var other => throw new InvalidOperationException($"No document {other} holds log channels.")
            };
            stored.ShouldBe(channel, type.ToString());
        }
    }

    [Fact]
    public void EachLogType_HasItsOwnField()
    {
        // Setting every log of a document to a distinct channel must keep them all distinct.
        var userLog = Enum.GetValues<GuildLogType>().Where(type => type.Document() == SettingsDocument.UserLog)
            .Aggregate(UserLogConfig.Empty, (document, type) =>
                document.WithLogChannel(type, new ChannelId((ulong)type + 1)));
        var messageLog = Enum.GetValues<GuildLogType>().Where(type => type.Document() == SettingsDocument.MessageLog)
            .Aggregate(MessageLogConfig.Empty, (document, type) =>
                document.WithLogChannel(type, new ChannelId((ulong)type + 1)));

        Enum.GetValues<GuildLogType>().Where(type => type.Document() == SettingsDocument.UserLog)
            .ShouldAllBe(type => userLog.LogChannel(type) == new ChannelId((ulong)type + 1));
        Enum.GetValues<GuildLogType>().Where(type => type.Document() == SettingsDocument.MessageLog)
            .ShouldAllBe(type => messageLog.LogChannel(type) == new ChannelId((ulong)type + 1));
    }

    [Fact]
    public void ALogTypeOfAnotherDocument_IsABug_ForReadsAndWrites()
    {
        foreach (var type in Enum.GetValues<GuildLogType>())
        {
            Refuses<GeneralConfig>(type);
            Refuses<LevelingConfig>(type);
            Refuses<MessageLogConfig>(type);
            Refuses<ModerationConfig>(type);
            Refuses<UserLogConfig>(type);
        }

        Should.Throw<InvariantViolationException>(() => GeneralConfig.Empty.LogChannel(GuildLogType.UserJoined))
            .Message.ShouldBe("The General document holds no channel for the UserJoined log.");
    }

    /// <summary>A snowflake written as a number, by hand or by an older tool, still reads.</summary>
    [Fact]
    public void ASnowflakeWrittenAsANumber_StillReads()
        => SettingsJson.Parse<GeneralConfig>("""{"UserCommandChannel":123}""").UserCommandChannel
            .ShouldBe(new ChannelId(123));

    [Fact]
    public void ANullDocument_IsABug()
        => Should.Throw<InvariantViolationException>(() => SettingsJson.Parse<ModuleStates>("null"))
            .Message.ShouldBe("A stored Modules settings document is null.");

    [Fact]
    public void ANullChannel_TurnsTheLogOff()
        => new MessageLogConfig { EditLogChannel = new ChannelId(5) }
            .WithLogChannel(GuildLogType.MessageEdited, null)
            .ShouldBe(MessageLogConfig.Empty);

    // ── LevelingConfig ────────────────────────────────────────────────────────

    [Fact]
    public void LevelingConfig_Resolve_FillsInTheDefaults()
    {
        var resolved = LevelingConfig.Empty.Resolve();

        resolved.Amount.ShouldBe(XpGainAmount.Default);
        resolved.Base.ShouldBe(LevelScalingBase.Default);
        resolved.Modifier.ShouldBe(LevelScalingModifier.Default);
        resolved.XpTimeoutPeriod.ShouldBe(XpTimeoutPeriod.Default);
    }

    [Fact]
    public void LevelingConfig_Resolve_KeepsEveryStoredValue()
    {
        var resolved = new LevelingConfig
        {
            XpGainAmount = XpGainAmount.FromDatabase(11),
            LevelScalingBase = LevelScalingBase.FromDatabase(22),
            LevelScalingModifier = LevelScalingModifier.FromDatabase(33),
            XpTimeout = XpTimeoutPeriod.FromDatabase(444)
        }.Resolve();

        resolved.Amount.Value.ShouldBe(11);
        resolved.Base.Value.ShouldBe(22);
        resolved.Modifier.Value.ShouldBe(33);
        resolved.XpTimeoutPeriod.Value.ShouldBe(TimeSpan.FromSeconds(444));
    }

    /// <summary>S-1: a stored value outside today's limits keeps the guild's curve.</summary>
    [Fact]
    public void LevelingConfig_Resolve_KeepsAStoredValueOutsideTodaysLimits()
        => new LevelingConfig { LevelScalingBase = LevelScalingBase.FromDatabase(1000) }.Resolve()
            .Base.Value.ShouldBe(1000);

    private static void Refuses<T>(GuildLogType type) where T : class, ILogChannelDocument<T>
    {
        if (type.Document() == T.Kind)
            return;
        Should.Throw<InvariantViolationException>(() => T.Empty.LogChannel(type), $"{T.Kind} reading {type}");
        Should.Throw<InvariantViolationException>(() => T.Empty.WithLogChannel(type, new ChannelId(1)),
            $"{T.Kind} writing {type}");
    }

    private static void RoundTrip<T>(T document) where T : class, ISettingsDocument<T>
        => SettingsJson.Parse<T>(SettingsJson.Serialize(document)).ShouldBe(document);

    private static ChannelId? Check<T>(GuildLogType type, ChannelId channel) where T : class, ILogChannelDocument<T>
    {
        var document = T.Empty.WithLogChannel(type, channel);
        return SettingsJson.Parse<T>(SettingsJson.Serialize(document)).LogChannel(type);
    }
}
