// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using LanguageExt;
using Microsoft.Extensions.Configuration;

namespace Grimoire.Features.Shared.PluralKit;

/// <summary>PluralKit's settings, read once (spec 1.5, section 3.8, KI-101).</summary>
public sealed record PluralKitOptions(Uri Endpoint, string UserAgent, string Token)
{
    public const string EndpointKey = "pluralkitApiEndpoint";
    public const string UserAgentKey = "pluralkitUserAgent";
    public const string TokenKey = "pluralkitToken";

    /// <summary>
    ///     Right when every key is set and the endpoint is an absolute http(s) URL; otherwise what's missing or wrong,
    ///     for the one startup warning. A blank value counts as missing. The endpoint gets a trailing <c>/</c>, so
    ///     <c>messages/{id}</c> resolves under <c>/v2/</c> and not beside it.
    /// </summary>
    public static Either<Seq<string>, PluralKitOptions> From(IConfiguration configuration)
    {
        var endpoint = Value(configuration, EndpointKey);
        var userAgent = Value(configuration, UserAgentKey);
        var token = Value(configuration, TokenKey);
        var uri = endpoint.Bind(ParseEndpoint);

        var problems = toSeq(new[]
            {
                endpoint.IsNone ? Some($"{EndpointKey} is missing")
                : uri.IsNone ? Some($"{EndpointKey} is not an absolute http(s) URL")
                : None,
                userAgent.IsNone ? Some($"{UserAgentKey} is missing") : None,
                token.IsNone ? Some($"{TokenKey} is missing") : None
            })
            .Somes();

        var options =
            from validEndpoint in uri
            from validUserAgent in userAgent
            from validToken in token
            select new PluralKitOptions(validEndpoint, validUserAgent, validToken);

        return options.Match(
            Some: Either.Right<Seq<string>, PluralKitOptions>,
            None: () => Either.Left<Seq<string>, PluralKitOptions>(problems));
    }

    private static Option<string> Value(IConfiguration configuration, string key) =>
        Optional(configuration[key]).Map(value => value.Trim()).Filter(value => value.Length > 0);

    private static Option<Uri> ParseEndpoint(string value) =>
        Uri.TryCreate(value.EndsWith('/') ? value : value + "/", UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? Some(uri)
            : None;
}
