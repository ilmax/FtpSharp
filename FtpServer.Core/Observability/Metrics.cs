using System.Diagnostics.Metrics;

namespace FtpServer.Core.Observability;

public static class Metrics
{
    public const string MeterName = "FtpServer";
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly UpDownCounter<long> SessionsActive = Meter.CreateUpDownCounter<long>(
        name: "ftp_sessions_active",
        unit: "sessions",
        description: "Number of active FTP control sessions");

    public static readonly Counter<long> CommandsTotal = Meter.CreateCounter<long>(
        name: "ftp_commands_total",
        unit: "count",
        description: "Total FTP commands processed");

    public static readonly Histogram<double> CommandDurationMs = Meter.CreateHistogram<double>(
        name: "ftp_command_duration_ms",
        unit: "ms",
        description: "Duration of FTP command handling in milliseconds");

    public static readonly Counter<long> BytesSent = Meter.CreateCounter<long>(
        name: "ftp_transfer_bytes_sent_total",
        unit: "bytes",
        description: "Total bytes sent to clients over data connections");

    public static readonly Counter<long> BytesReceived = Meter.CreateCounter<long>(
        name: "ftp_transfer_bytes_received_total",
        unit: "bytes",
        description: "Total bytes received from clients over data connections");

    public static readonly Counter<long> ErrorsTotal = Meter.CreateCounter<long>(
        name: "ftp_errors_total",
        unit: "count",
        description: "Total number of errors encountered");

    // Session-level metrics (low-cardinality tags only; e.g., a bounded session_id or none)
    public static readonly Counter<long> SessionBytesSent = Meter.CreateCounter<long>(
        name: "ftp_session_bytes_sent_total",
        unit: "bytes",
        description: "Bytes sent over data connections attributed to a session");

    public static readonly Counter<long> SessionBytesReceived = Meter.CreateCounter<long>(
        name: "ftp_session_bytes_received_total",
        unit: "bytes",
        description: "Bytes received over data connections attributed to a session");

    public static readonly UpDownCounter<long> SessionActiveTransfers = Meter.CreateUpDownCounter<long>(
        name: "ftp_session_active_transfers",
        unit: "transfers",
        description: "Active data transfers per session");
}
