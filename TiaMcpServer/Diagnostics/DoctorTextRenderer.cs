using System.Text;

namespace TiaMcpServer.Diagnostics;

/// <summary>Human-readable rendering (the doctor CLI + MCP tool default).</summary>
public static class DoctorTextRenderer
{
    public static string Render(DoctorReport report)
    {
        var builder = new StringBuilder();
        using var writer = new StringWriter(builder);
        Render(report, writer);
        return builder.ToString();
    }

    public static void Render(DoctorReport report, TextWriter writer)
    {
        writer.WriteLine($"TIA Portal MCP doctor — host {report.HostVersion}");
        writer.WriteLine();

        foreach (var check in report.Checks)
        {
            writer.WriteLine($"{Tag(check.Status)} {check.Id}");
            writer.WriteLine($"       {check.Summary}");
            foreach (var line in check.Evidence)
            {
                writer.WriteLine($"         - {line}");
            }

            writer.WriteLine();
        }

        var (ok, info, warn, fail) = DoctorCounts.Of(report);
        writer.WriteLine($"Summary: {ok} ok, {info} info, {warn} warn, {fail} fail");
        writer.WriteLine(report.Status switch
        {
            DiagnosticStatus.Fail => "NOT READY — fix the [FAIL] checks above (their evidence says how).",
            DiagnosticStatus.Warn => "Ready with warnings — review the [WARN] checks.",
            _ => "Ready."
        });
    }

    private static string Tag(DiagnosticStatus status)
        => status switch
        {
            DiagnosticStatus.Ok => "[OK]  ",
            DiagnosticStatus.Info => "[INFO]",
            DiagnosticStatus.Warn => "[WARN]",
            DiagnosticStatus.Fail => "[FAIL]",
            _ => "[?]   "
        };
}
