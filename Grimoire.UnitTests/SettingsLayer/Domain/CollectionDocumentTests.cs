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

/// <summary>Spec 2.5, section 8: the five collection documents and their JSON.</summary>
public sealed class CollectionDocumentTests
{
    private static readonly ulong _largest = long.MaxValue;

    private static LevelRewards Rewards()
        => LevelRewards.Empty
            .Set(new RewardEntry(new RoleId(2), 5, RewardMessage.FromDatabase("Well done"))).Document
            .Set(new RewardEntry(new RoleId(1), 3, null)).Document;

    private static XpExclusions Exclusions()
        => XpExclusions.Empty.Add([
            new XpTarget.Role(new RoleId(30)), new XpTarget.Channel(new ChannelId(10)),
            new XpTarget.Member(new UserId(20)), new XpTarget.Role(new RoleId(3))
        ]).Document;

    private static DiagnosticsIgnoreList Ignores()
        => DiagnosticsIgnoreList.Empty.Add([
            new DiagnosticsTarget.Member(new UserId(7)), new DiagnosticsTarget.Role(new RoleId(8))
        ]).Document;

    private static MessageLogOverrides LogOverrides()
        => MessageLogOverrides.Empty
            .With(new ChannelId(2), MessageLogOverrideOption.NeverLog)
            .With(new ChannelId(1), MessageLogOverrideOption.AlwaysLog);

    private static SpamFilterOverrides SpamOverrides()
        => SpamFilterOverrides.Empty
            .With(new ChannelId(2), SpamFilterOverrideOption.NeverFilter)
            .With(new ChannelId(1), SpamFilterOverrideOption.AlwaysFilter);

    // ── JSON ──────────────────────────────────────────────────────────────────

    [Fact]
    public void EachDocument_RoundTripsThroughItsJson()
    {
        RoundTrip(Rewards());
        RoundTrip(Exclusions());
        RoundTrip(Ignores());
        RoundTrip(LogOverrides());
        RoundTrip(SpamOverrides());
    }

    [Fact]
    public void AnEmptyObject_ReadsAsEmpty()
    {
        SettingsJson.Parse<LevelRewards>("{}").ShouldBe(LevelRewards.Empty);
        SettingsJson.Parse<XpExclusions>("{}").ShouldBe(XpExclusions.Empty);
        SettingsJson.Parse<DiagnosticsIgnoreList>("{}").ShouldBe(DiagnosticsIgnoreList.Empty);
        SettingsJson.Parse<MessageLogOverrides>("{}").ShouldBe(MessageLogOverrides.Empty);
        SettingsJson.Parse<SpamFilterOverrides>("{}").ShouldBe(SpamFilterOverrides.Empty);
    }

    /// <summary>An empty collection isn't written, and removing the last entry gets back to <c>{}</c>.</summary>
    [Fact]
    public void AnEmptyCollection_IsLeftOut()
    {
        SettingsJson.Serialize(LevelRewards.Empty).ShouldBe("{}");
        SettingsJson.Serialize(XpExclusions.Empty).ShouldBe("{}");
        SettingsJson.Serialize(MessageLogOverrides.Empty).ShouldBe("{}");
        SettingsJson.Serialize(Rewards().Remove([new RoleId(1), new RoleId(2)]).Document).ShouldBe("{}");
        SettingsJson.Serialize(XpExclusions.Empty.Add([new XpTarget.Member(new UserId(1))]).Document)
            .ShouldBe("""{"Members":["1"]}""");
    }

    /// <summary>
    ///     The shapes the legacy copy writes (spec 2.5, section 6.3): keys and elements as strings, sorted by id, and
    ///     the override options by name.
    /// </summary>
    [Fact]
    public void TheJson_HasTheShapeTheCopyWrites()
    {
        SettingsJson.Serialize(Rewards())
            .ShouldBe("""{"Roles":{"1":{"Level":3},"2":{"Level":5,"Message":"Well done"}}}""");
        SettingsJson.Serialize(Exclusions())
            .ShouldBe("""{"Channels":["10"],"Members":["20"],"Roles":["3","30"]}""");
        SettingsJson.Serialize(Ignores()).ShouldBe("""{"Roles":["8"],"Members":["7"]}""");
        SettingsJson.Serialize(LogOverrides()).ShouldBe("""{"Channels":{"1":"AlwaysLog","2":"NeverLog"}}""");
        SettingsJson.Serialize(SpamOverrides()).ShouldBe("""{"Channels":{"1":"AlwaysFilter","2":"NeverFilter"}}""");
    }

    /// <summary>Ids sort by number, not as text: "3" before "30", and "9" before "10".</summary>
    [Fact]
    public void Ids_AreSortedByNumber()
        => SettingsJson.Serialize(XpExclusions.Empty.Add([
                new XpTarget.Channel(new ChannelId(10)), new XpTarget.Channel(new ChannelId(9))
            ]).Document)
            .ShouldBe("""{"Channels":["9","10"]}""");

    [Fact]
    public void ASnowflakeKeyAndElement_AreStringsAndExact()
    {
        var rewards = LevelRewards.Empty.Set(new RewardEntry(new RoleId(_largest), 1, null)).Document;
        var exclusions = XpExclusions.Empty.Add([new XpTarget.Member(new UserId(_largest))]).Document;

        SettingsJson.Serialize(rewards).ShouldBe("""{"Roles":{"9223372036854775807":{"Level":1}}}""");
        SettingsJson.Parse<LevelRewards>(SettingsJson.Serialize(rewards)).ShouldBe(rewards);
        SettingsJson.Parse<XpExclusions>(SettingsJson.Serialize(exclusions)).Members
            .ShouldBe(toSet([new UserId(_largest)]));
    }

    /// <summary>
    ///     The same content added in another order is equal and writes the same JSON, which the "unchanged" check and
    ///     the copy's equivalence rely on (spec 2.5, section 3.7).
    /// </summary>
    [Fact]
    public void TheSameContent_InAnotherOrder_IsEqualAndWritesTheSameJson()
    {
        var other = XpExclusions.Empty.Add([
            new XpTarget.Role(new RoleId(3)), new XpTarget.Member(new UserId(20)),
            new XpTarget.Channel(new ChannelId(10)), new XpTarget.Role(new RoleId(30))
        ]).Document;

        other.ShouldBe(Exclusions());
        SettingsJson.Serialize(other).ShouldBe(SettingsJson.Serialize(Exclusions()));
        LogOverrides().ShouldBe(MessageLogOverrides.Empty
            .With(new ChannelId(1), MessageLogOverrideOption.AlwaysLog)
            .With(new ChannelId(2), MessageLogOverrideOption.NeverLog));
    }

    /// <summary>A collection emptied by removals equals one that never had anything.</summary>
    [Fact]
    public void AnEmptiedCollection_EqualsEmpty()
    {
        Rewards().Remove([new RoleId(1), new RoleId(2)]).Document.ShouldBe(LevelRewards.Empty);
        Exclusions().Remove(Exclusions().Targets).Document.ShouldBe(XpExclusions.Empty);
        LogOverrides().With(new ChannelId(1), None).With(new ChannelId(2), None).ShouldBe(MessageLogOverrides.Empty);
    }

    [Fact]
    public void ADocumentThatDoesNotParse_Fails()
    {
        Should.Throw<JsonException>(() => SettingsJson.Parse<XpExclusions>("""{"Channels":"not an array"}"""));
        Should.Throw<JsonException>(() => SettingsJson.Parse<LevelRewards>("""{"Roles":["not an object"]}"""));
        Should.Throw<JsonException>(() =>
            SettingsJson.Parse<MessageLogOverrides>("""{"Channels":{"1":"Inherit"}}"""));
    }

    /// <summary>A stored null where an entry or a collection belongs is corrupt data, never read as empty.</summary>
    [Fact]
    public void ANullEntryOrCollection_Fails_WithWhatWasWrong()
    {
        Should.Throw<JsonException>(() => SettingsJson.Parse<LevelRewards>("""{"Roles":{"1":null}}"""))
            .Message.ShouldBe("A map holds a null value.");
        Should.Throw<JsonException>(() => SettingsJson.Parse<XpExclusions>("""{"Members":null}"""))
            .Message.ShouldBe("A set is null.");
        Should.Throw<JsonException>(() => SettingsJson.Parse<MessageLogOverrides>("""{"Channels":[]}"""))
            .Message.ShouldBe("Expected an object for a map, found StartArray.");
    }

    [Fact]
    public void AnUnknownField_IsIgnored()
        => SettingsJson.Parse<XpExclusions>("""{"Members":["1"],"Categories":["2"]}""")
            .ShouldBe(XpExclusions.Empty.Add([new XpTarget.Member(new UserId(1))]).Document);

    private static void RoundTrip<T>(T document) where T : class, ISettingsDocument<T>
        => SettingsJson.Parse<T>(SettingsJson.Serialize(document)).ShouldBe(document);
}
