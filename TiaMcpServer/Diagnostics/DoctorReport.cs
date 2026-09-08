namespace TiaMcpServer.Diagnostics;

/// <summary>Aggregate doctor report. <see cref="Status"/> is the worst check status
/// (Fail &gt; Warn &gt; Info &gt; Ok). Counts are derived, not stored.</summary>
public sealed record DoctorReport(
    DiagnosticStatus Status,
    DateTimeOffset TimestampUtc,
    string HostVersion,
    IReadOnlyList<DiagnosticCheckResult> Checks);

/// <summary>Derived per-status counts for a report (used by renderers).</summary>
public static class DoctorCounts
{
    public static (int Ok, int Info, int Warn, int Fail) Of(DoctorReport report)
        => (
            report.Checks.Count(c => c.Status == DiagnosticStatus.Ok),
            report.Checks.Count(c => c.Status == DiagnosticStatus.Info),
            report.Checks.Count(c => c.Status == DiagnosticStatus.Warn),
            report.Checks.Count(c => c.Status == DiagnosticStatus.Fail));
}
