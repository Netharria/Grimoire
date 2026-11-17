# Telemetry runbook

How Grimoire's traces, metrics and logs get from the bot to Grafana, how to set up each end, and how to use them.
The design is in [spec 1.6](../langext-migration/specs/1.6-telemetry.md); the code is described in
[`architecture.md`](../architecture.md), section 8.

```
DigitalOcean App Platform (the bot)          home server
───────────────────────────────────          ───────────
OTLP/HTTP, gzip, basic auth  ──HTTPS──►  Nginx Proxy Manager ──► otel-lgtm
(background threads, bounded queues)     otlp.<domain>           collector (tail sampling) → Tempo · Loki · Mimir
console logs ──► App Platform's logs     grafana.<domain> ──────► Grafana :3000
```

The bot sends straight to home: App Platform has no sidecars, and its disk is wiped on every deploy. If home is down,
the bot keeps a few minutes of traces and logs in memory, then drops new ones (spec 1.6, OQ-3). Metrics recover their
totals at the next export. App Platform's console logs always have every line, with its trace id.

## 1. The bot (App Platform)

Set these as app-level environment variables. Encrypt the header.

| Variable | Value |
|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `https://otlp.<domain>` (no path: the exporters add `/v1/traces`, `/v1/metrics`, `/v1/logs`) |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` (the code sets it too) |
| `OTEL_EXPORTER_OTLP_HEADERS` | `Authorization=Basic <base64 of user:password>`, encrypted |
| `OTEL_EXPORTER_OTLP_TIMEOUT` | `10000` |
| `OTEL_RESOURCE_ATTRIBUTES` | optional, for example `deployment.environment.name=production` |
| `telemetryTraceUrl` | optional: a Grafana link with `{traceId}` in it, added to alerts. See section 4 |

Without `OTEL_EXPORTER_OTLP_ENDPOINT`, nothing is exported and the bot logs "Telemetry export is off" once at
startup. Spans still run in process, so the error id in a reply is still a trace id.

To make the header value: `printf 'user:password' | base64`.

**When export fails** the bot logs "Telemetry export problem (…, at most once a minute)" at Warning, from the
OpenTelemetry SDK's own reports (dropped spans, failed exports). Look for it in App Platform's runtime logs.

## 2. Home: otel-lgtm

Run `grafana/otel-lgtm` (pin the tag) with its data on a volume, so a restart keeps everything:

```yaml
services:
  otel-lgtm:
    image: grafana/otel-lgtm:<pinned tag>
    restart: unless-stopped
    volumes:
      - otel-lgtm-data:/data
      - ./otelcol-config.yaml:/otel-lgtm/otelcol-config.yaml:ro   # with tail sampling, see below
    environment:
      GF_SECURITY_ADMIN_PASSWORD: <change me>
    # No published ports: NPM reaches it on the shared Docker network.
volumes:
  otel-lgtm-data:
```

**Tail sampling:** merge [`deploy/otel-lgtm/tail-sampling.yaml`](../../deploy/otel-lgtm/tail-sampling.yaml) into
the image's own collector config, as its header says. It keeps every trace except a worker tick that succeeded and
did nothing. Check the config path and the policy keys against the image's collector version.

**Retention** (spec 1.6, OQ-8): traces 90 days, logs 90 days, metrics 2 years. Each is set in its backend's config
inside the image (Tempo's `compactor.compaction.block_retention`, Loki's `limits_config.retention_period` with the
compactor's retention on, and Mimir's or Prometheus's retention). Copy each file out the same way as the collector's,
edit it, and mount it back. Check how much disk a week uses before trusting the numbers.

`otel-lgtm` is published for development and demos. It works for one owner, but upgrades are manual: pin the tag,
and back up the volume before changing it.

## 3. Home: Nginx Proxy Manager

Two proxy hosts. Never publish 4317, 4318 or 3000 directly.

| Host | Forwards to | Protection |
|---|---|---|
| `otlp.<domain>` | `otel-lgtm:4318` (HTTP) | TLS (Let's Encrypt), an access list with basic auth (the user and password in the bot's header). Raise the client body size to at least 8 MB (Advanced: `client_max_body_size 8m;`), so a big batch isn't refused with 413 |
| `grafana.<domain>` | `otel-lgtm:3000` | TLS, Grafana's own login with the admin password changed. An access list as well if you like |

Test the OTLP route from anywhere: `curl -i -u user:password -X POST https://otlp.<domain>/v1/traces -H
'Content-Type: application/x-protobuf' --data-binary ''` should answer 200 (an empty export), and 401 without `-u`.

## 4. Using it

**From a user's report.** A user who got "Something went wrong" quotes a 32-character id. In Grafana, Explore →
Tempo → TraceQL, paste the id. The trace shows the command, each database and Discord call under it, and which one
failed, with its status, Postgres code or Discord code. The logs for the same trace are one click away (Loki, by
`trace_id`).

**From an alert.** Urgent and warning alerts for commands, events and workers carry "[Id …]", the same trace id. With
`telemetryTraceUrl` set, for example `https://grafana.<domain>/explore?left={"datasource":"tempo","queries":[{"query":"{traceId}","queryType":"traceql"}]}`
(adjust to your Grafana's data source name), the alert has a "View trace" link.

**Starter dashboards** (build in Grafana, from Mimir):

| Panel | Query idea |
|---|---|
| Command latency, p50/p95, by command | `histogram_quantile(0.95, sum by (le, grimoire_command) (rate(grimoire_command_duration_seconds_bucket[5m])))` |
| Error rate | `grimoire_outcome` of `Unavailable` and `Defect` over all, from the duration histograms' counts |
| Outages by dependency | `sum by (grimoire_dependency, grimoire_operation) (rate(grimoire_call_duration_seconds_count{grimoire_outcome="Unavailable"}[5m]))` |
| Retries | `rate(grimoire_call_retries_total[5m])` by dependency |
| Queues | `grimoire_guildlog_queue_depth`, `grimoire_pluralkit_queue_depth` |
| Gateway | `grimoire_gateway_disconnected_seconds` |
| Database pool | Npgsql's `db_client_connection_count` by state, pool `grimoire` |
| Cache hit rate | FusionCache's hit and miss counters |

The exact metric names depend on how Mimir translates OTLP names (dots become underscores, units and `_total` are
added). Check them in Explore first.

**Alerts worth setting in Grafana:** no `grimoire_command_duration_seconds_count` increase for 30 minutes (the bot or
the export is down); `grimoire_gateway_disconnected_seconds > 120`; any `Defect` outcome.
