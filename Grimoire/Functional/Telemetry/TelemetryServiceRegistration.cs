// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reflection;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.Gateway;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.PluralKit;
using LanguageExt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using ZiggyCreatures.Caching.Fusion.OpenTelemetry;

namespace Grimoire.Functional.Telemetry;

/// <summary>
///     OpenTelemetry for the bot (spec 1.6, section 3.10). Spans and metrics always run in process, so the error id is
///     a trace id even in development. They're exported only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set: straight
///     to the home server, since App Platform has no sidecars. The exporters send from background threads with
///     bounded queues and never block the bot; a full queue drops the newest data (OQ-3).
/// </summary>
internal static class TelemetryServiceRegistration
{
    public const string EndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";
    public const string ServiceName = "grimoire";

    /// <summary>Spans the trace exporter holds while home is slow or down: a few minutes of a busy bot.</summary>
    private const int TraceQueue = 16_384;

    /// <summary>Log events Serilog's sink holds, to match.</summary>
    private const int LogQueue = 20_000;

    /// <summary>The sources whose spans are recorded: ours, and the libraries' that nest under them.</summary>
    internal static readonly string[] Sources = [TelemetryNames.Source, "Npgsql", RedactingProcessor.HttpSource];

    internal static readonly string[] Meters =
        [TelemetryNames.Source, "Npgsql", "System.Net.Http", "System.Runtime"];

    /// <summary>The endpoint, when export is on. Read from the configuration, which includes the environment.</summary>
    public static Option<string> Endpoint(IConfiguration configuration) =>
        Optional(configuration[EndpointVariable]).Filter(value => !string.IsNullOrWhiteSpace(value));

    extension(IServiceCollection services)
    {
        public IServiceCollection AddTelemetry(IConfiguration configuration)
        {
            var exporting = Endpoint(configuration).IsSome;
            var version = Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

            services.AddSingleton(_ => new ActivitySource(TelemetryNames.Source, version))
                .AddSingleton(_ => new GrimoireMetrics())
                .AddHostedService<TelemetryGauges>();
            if (exporting)
                services.AddHostedService<ExportHealth>();

            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(ServiceName, serviceVersion: version, autoGenerateServiceInstanceId: true)
                    .AddAttributes([
                        new KeyValuePair<string, object>("deployment.environment.name",
                            configuration["DOTNET_ENVIRONMENT"] ?? Environments.Production)
                    ]))
                .WithTracing(tracing =>
                {
                    tracing.SetSampler(new GrimoireSampler())
                        .AddSource(Sources)
                        .AddProcessor(new RedactingProcessor());
                    if (exporting)
                        tracing.AddOtlpExporter(Configure);
                })
                .WithMetrics(metrics =>
                {
                    metrics.AddMeter(Meters)
                        .AddFusionCacheInstrumentation();
                    if (exporting)
                        metrics.AddOtlpExporter((exporter, reader) =>
                        {
                            Configure(exporter);
                            reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 30_000;
                        });
                });
            return services;
        }
    }

    /// <summary>
    ///     Adds the OTLP log sink when export is on. The endpoint, headers and protocol come from the same
    ///     <c>OTEL_EXPORTER_OTLP_*</c> variables the trace and metric exporters read.
    /// </summary>
    public static LoggerConfiguration WithTelemetry(this LoggerConfiguration logger, IConfiguration configuration) =>
        Endpoint(configuration).IsNone
            ? logger
            : logger.WriteTo.OpenTelemetry(options =>
            {
                options.Protocol = OtlpProtocol.HttpProtobuf;
                options.ResourceAttributes = new Dictionary<string, object> { ["service.name"] = ServiceName };
                options.BatchingOptions.QueueLimit = LogQueue;
                // The sink's own requests are not Grimoire's work; the sampler drops them anyway.
                options.OnBeginSuppressInstrumentation = SuppressInstrumentationScope.Begin;
            });

    /// <summary>
    ///     HTTP with protobuf, so the path is the standard /v1/traces under the endpoint and a reverse proxy can route
    ///     it. The endpoint and the auth header come from the environment.
    /// </summary>
    private static void Configure(OtlpExporterOptions exporter)
    {
        exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
        exporter.Compression = OtlpExportCompression.GZip;
        exporter.BatchExportProcessorOptions.MaxQueueSize = TraceQueue;
        exporter.BatchExportProcessorOptions.MaxExportBatchSize = 512;
        exporter.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 5_000;
    }
}

/// <summary>
///     The observable gauges (spec 1.6, section 7). Registered once the host starts, since their sources (the guild
///     log queue, the PluralKit bucket, the gateway) are singletons resolved on first use. Also says once, at startup,
///     when nothing is exported.
/// </summary>
internal sealed partial class TelemetryGauges(
    GrimoireMetrics metrics,
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<TelemetryGauges> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        metrics.Meter.CreateObservableGauge(TelemetryNames.GuildLogQueueDepth,
            () => services.GetService<GuildLog>()?.QueuedCount ?? 0, "{message}", "Guild log posts waiting");
        metrics.Meter.CreateObservableGauge(TelemetryNames.PluralKitQueueDepth,
            () => (services.GetService<IPluralKitIO>() as LivePluralKitIO)?.Statistics?.CurrentQueuedCount ?? 0,
            "{lookup}", "PluralKit lookups waiting for the rate limiter");
        metrics.Meter.CreateObservableGauge(TelemetryNames.GatewayDisconnected,
            () => services.GetService<GatewayHealth>()?.DisconnectedFor()?.TotalSeconds ?? 0, "s",
            "How long the Discord gateway has been down; 0 while connected");

        if (TelemetryServiceRegistration.Endpoint(configuration).IsNone)
            LogExportOff(logger, TelemetryServiceRegistration.EndpointVariable);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(LogLevel.Information, "Telemetry export is off: {Variable} is not set.")]
    private static partial void LogExportOff(ILogger logger, string variable);
}
