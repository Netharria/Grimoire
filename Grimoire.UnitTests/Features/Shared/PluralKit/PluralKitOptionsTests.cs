// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.PluralKit;
using LanguageExt;
using Microsoft.Extensions.Configuration;

namespace Grimoire.UnitTests.Features.Shared.PluralKit;

/// <summary>Spec 1.5, section 3.8 (KI-101): PluralKit is on only when every key is set.</summary>
public sealed class PluralKitOptionsTests
{
    private static Either<Seq<string>, PluralKitOptions> From(string? endpoint = "https://api.pluralkit.me/v2/",
        string? userAgent = "Grimoire (test)", string? token = "token") =>
        PluralKitOptions.From(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [PluralKitOptions.EndpointKey] = endpoint,
            [PluralKitOptions.UserAgentKey] = userAgent,
            [PluralKitOptions.TokenKey] = token
        }).Build());

    private static PluralKitOptions ShouldBeOn(Either<Seq<string>, PluralKitOptions> result) =>
        result.Match(Right: options => options,
            Left: problems => throw new ShouldAssertException($"expected on, got: {string.Join("; ", problems)}"));

    private static Seq<string> ShouldBeOff(Either<Seq<string>, PluralKitOptions> result) =>
        result.Match(Right: _ => throw new ShouldAssertException("expected off"), Left: problems => problems);

    [Fact]
    public void EveryKeySet_IsOn()
    {
        var options = ShouldBeOn(From());

        options.Endpoint.ShouldBe(new Uri("https://api.pluralkit.me/v2/"));
        options.UserAgent.ShouldBe("Grimoire (test)");
        options.Token.ShouldBe("token");
    }

    [Fact]
    public void AnEndpointWithoutATrailingSlash_GetsOne_SoMessagesResolveUnderV2()
    {
        var options = ShouldBeOn(From(endpoint: "https://api.pluralkit.me/v2"));

        new Uri(options.Endpoint, "messages/1").ToString().ShouldBe("https://api.pluralkit.me/v2/messages/1");
    }

    [Fact]
    public void Values_AreTrimmed()
        => ShouldBeOn(From(token: "  token  ")).Token.ShouldBe("token");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingEndpoint_IsOff_AndSaysSo(string? endpoint)
        => ShouldBeOff(From(endpoint: endpoint)).ShouldBe(["pluralkitApiEndpoint is missing"]);

    [Theory]
    [InlineData("api.pluralkit.me/v2/")]
    [InlineData("/v2/")]
    [InlineData("ftp://api.pluralkit.me/v2/")]
    public void AnEndpointThatIsntAnAbsoluteHttpUrl_IsOff(string endpoint)
        => ShouldBeOff(From(endpoint: endpoint)).ShouldBe(["pluralkitApiEndpoint is not an absolute http(s) URL"]);

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void AMissingUserAgent_IsOff(string? userAgent)
        => ShouldBeOff(From(userAgent: userAgent)).ShouldBe(["pluralkitUserAgent is missing"]);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AMissingToken_IsOff(string? token)
        => ShouldBeOff(From(token: token)).ShouldBe(["pluralkitToken is missing"]);

    [Fact]
    public void NothingSet_NamesEveryKey()
        => ShouldBeOff(From(null, null, null)).ShouldBe([
            "pluralkitApiEndpoint is missing", "pluralkitUserAgent is missing", "pluralkitToken is missing"
        ]);
}
