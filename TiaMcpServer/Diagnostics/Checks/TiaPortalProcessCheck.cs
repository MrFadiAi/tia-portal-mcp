namespace TiaMcpServer.Diagnostics.Checks;

/// <summary>How many TIA Portal processes are running. Zero is Info (nothing wrong — no project
/// is open), so the doctor stays green on an idle machine while still showing the fact.</summary>
public sealed class TiaPortalProcessCheck(IProcessLister processLister) : IDiagnosticCheck
{
    public const string ProcessPrefix = "Siemens.Automation.Portal";

    public string Id => "tia-processes";

    public DiagnosticCheckResult Run()
    {
        var count = CountTiaProcesses(processLister.GetProcessNames());
        return count == 0
            ? DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Info,
                "No TIA Portal process is running (tools that need an open project will not work until one is).",
                new[] { "processes: 0" })
            : DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Ok,
                $"{count} TIA Portal process(es) running.",
                new[] { $"processes: {count}" });
    }

    /// <summary>Pure: count process names starting with the TIA Portal executable prefix.</summary>
    internal static int CountTiaProcesses(IEnumerable<string> processNames)
        => processNames.Count(p => p.StartsWith(ProcessPrefix, StringComparison.OrdinalIgnoreCase));
}
