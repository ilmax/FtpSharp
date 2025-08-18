# Metrics and Health

This project exposes basic metrics via .NET `System.Diagnostics.Metrics` (OpenTelemetry-friendly) and an optional lightweight HTTP health endpoint.

## Metrics

The following instruments are registered under meter `FtpServer`:

- UpDownCounter `ftp_sessions_active` — active control sessions.
- Counter `ftp_commands_total` — total commands processed (tag: `command`).
- Histogram `ftp_command_duration_ms` — command handler duration (ms) (tag: `command`).
- Counter `ftp_transfer_bytes_sent_total` — bytes sent over data connections.
- Counter `ftp_transfer_bytes_received_total` — bytes received over data connections.
- Counter `ftp_errors_total` — error count.

### Exporting metrics

Use an OpenTelemetry Metrics provider in your host to export these metrics. For example, to export to console:

```csharp
// In Program.cs (example snippet)
using OpenTelemetry;
using OpenTelemetry.Metrics;

builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m
        .AddMeter("FtpServer")
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddConsoleExporter());
```

Or export to Prometheus using the OpenTelemetry.Exporter.Prometheus.AspNetCore package in an ASP.NET Core host.

## Health Endpoint

A lightweight `HttpListener` can serve health:

- Enable via CLI or configuration.
  - CLI: `--health true --health-url http://127.0.0.1:8080/`
  - Env: `FTP_FtpServer__HealthEnabled=true`, `FTP_FtpServer__HealthUrl=http://127.0.0.1:8080/`
- Endpoint:
  - `GET /health` → `200 OK` with body `OK`.

For metrics, use the Prometheus exporter exposed by the ASP.NET host at `/metrics`.

## Troubleshooting

- Port in use: change `--health-url` to a free port.
- No metrics visible: ensure an exporter is configured and you added meter `FtpServer`.
