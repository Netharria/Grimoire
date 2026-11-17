// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Grimoire.Functional.Telemetry;

/// <summary>
///     Which spans are recorded (spec 1.6, section 3.5). A span with a parent follows it. A span with no parent is kept
///     only when it's one of our roots, which carry <c>grimoire.root</c>. Everything else with no parent is dropped:
///     Npgsql's spans from unported EF code, HttpClient spans DSharpPlus sends on its own flow, our own capability
///     spans in a bridge, and the exporters' own requests.
/// </summary>
internal sealed class GrimoireSampler : Sampler
{
    private static readonly SamplingResult Keep = new(SamplingDecision.RecordAndSample);
    private static readonly SamplingResult Drop = new(SamplingDecision.Drop);

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
        Decide(samplingParameters.ParentContext, samplingParameters.Tags) ? Keep : Drop;

    /// <summary>The rule itself. Pure.</summary>
    internal static bool Decide(ActivityContext parent, IEnumerable<KeyValuePair<string, object?>>? tags) =>
        parent.TraceId != default
            ? parent.TraceFlags.HasFlag(ActivityTraceFlags.Recorded)
            : KeepRoot(tags);

    internal static bool KeepRoot(IEnumerable<KeyValuePair<string, object?>>? tags) =>
        tags?.Any(tag => tag is { Key: TelemetryNames.Root, Value: true }) ?? false;
}

/// <summary>
///     Discord puts the interaction or webhook token in the URL path, which .NET doesn't redact (it redacts query
///     strings only). A token is valid for 15 minutes, so it never leaves the process (spec 1.6, section 3.6). Pure.
/// </summary>
internal static partial class UrlRedaction
{
    public const string Placeholder = "{token}";

    public static string Redact(string url) =>
        TokenSegment().Replace(url, match => $"/{match.Groups["kind"].Value}/{match.Groups["id"].Value}/{Placeholder}");

    [GeneratedRegex(@"/(?<kind>interactions|webhooks)/(?<id>\d+)/(?<token>[^/?#]+)", RegexOptions.CultureInvariant)]
    private static partial Regex TokenSegment();
}

/// <summary>Runs <see cref="UrlRedaction" /> over every HttpClient span before an exporter sees it.</summary>
internal sealed class RedactingProcessor : BaseProcessor<Activity>
{
    internal const string HttpSource = "System.Net.Http";
    private const string UrlFull = "url.full";

    public override void OnEnd(Activity data)
    {
        if (data.Source.Name != HttpSource || data.GetTagItem(UrlFull) is not string url)
            return;

        var redacted = UrlRedaction.Redact(url);
        if (!ReferenceEquals(redacted, url) && redacted != url)
            data.SetTag(UrlFull, redacted);
    }
}
