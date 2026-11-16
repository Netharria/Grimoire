// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.PluralKit;
using Grimoire.Features.Shared.PluralKit.Models;
using Grimoire.Functional.PluralKit;

namespace Grimoire.UnitTests.Features.Shared.PluralKit;

/// <summary>Spec 1.5, section 3.2: what PluralKit's answer means for the deleted message (LOG-4, Q37).</summary>
public sealed class PluralKitMessageTests
{
    private static readonly MessageId Original = new(100);
    private static readonly MessageId Proxy = new(200);

    private static PluralKitMessage Answer(string? id = "200", string? original = "100", string? system = "abcde",
        string? member = "fghij") =>
        new(id, original, system is null ? null : new PluralKitMessage.Entity(system),
            member is null ? null : new PluralKitMessage.Entity(member));

    private static void ShouldBeUnreadable(Error error)
    {
        error.ShouldBeOfType<Unavailable>();
        PluralKitErrors.ProblemOf(error).ShouldBeSome().ShouldBe(PluralKitProblem.Unreadable);
    }

    [Fact]
    public void TheOriginal_ReplacedByAProxy_IsLinked()
    {
        var link = Answer().Classify(Original).ShouldSucceed()
            .ShouldBeOfType<ProxyLookup.ReplacedByProxy>().Link;

        link.ProxyMessageId.ShouldBe(Proxy);
        link.OriginalMessageId.ShouldBe(Original);
        link.SystemId.Value.ShouldBe("abcde");
        link.MemberId.Value.ShouldBe("fghij");
    }

    [Fact]
    public void TheProxyItself_IsTheProxy()
        => Answer().Classify(Proxy).ShouldSucceed().ShouldBeOfType<ProxyLookup.IsTheProxy>();

    [Fact]
    public void TheProxyItself_IsTheProxy_EvenWhenItsMemberWasDeleted()
        => Answer(system: null, member: null).Classify(Proxy).ShouldSucceed().ShouldBeOfType<ProxyLookup.IsTheProxy>();

    [Theory]
    [InlineData(null, null)]
    [InlineData("abcde", null)]
    [InlineData(null, "fghij")]
    public void NoSystemOrMember_MeansTheMemberWasDeleted(string? system, string? member)
        => Answer(system: system, member: member).Classify(Original).ShouldSucceed()
            .ShouldBeOfType<ProxyLookup.ReplacedByProxyOfDeletedMember>().Proxy.ShouldBe(Proxy);

    [Fact]
    public void AnEntityWithNoId_MeansTheMemberWasDeleted()
        => new PluralKitMessage("200", "100", new PluralKitMessage.Entity(null), new PluralKitMessage.Entity("fghij"))
            .Classify(Original).ShouldSucceed().ShouldBeOfType<ProxyLookup.ReplacedByProxyOfDeletedMember>();

    [Theory]
    [InlineData(" ", "fghij")]
    [InlineData("abcde", "")]
    public void ABlankId_IsUnreadable(string system, string member)
        => ShouldBeUnreadable(Answer(system: system, member: member).Classify(Original).ShouldFail());

    [Theory]
    [InlineData(null, "100")]
    [InlineData("200", null)]
    [InlineData("abc", "100")]
    [InlineData("200", "-1")]
    [InlineData("0", "100")]
    [InlineData("200", " 100")]
    public void IdsThatArentSnowflakes_AreUnreadable(string? id, string? original)
        => ShouldBeUnreadable(Answer(id, original).Classify(Original).ShouldFail());

    [Fact]
    public void AnAnswerAboutAnotherMessage_IsUnreadable_SoNoWrongLinkIsSaved()
        => ShouldBeUnreadable(Answer(original: "999").Classify(Original).ShouldFail());
}
