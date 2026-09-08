namespace TiaMcpServer.Diagnostics;

/// <summary>A single self-diagnosis check. Must not throw for expected conditions — the runner
/// also guards unexpected ones so one broken check never blanks the whole report.</summary>
public interface IDiagnosticCheck
{
    string Id { get; }

    DiagnosticCheckResult Run();
}

/// <summary>Runs every check, converts a throwing check into a Fail result, and aggregates the
/// worst status. Entirely host-side: no Siemens DLLs, no TIA Portal interaction — the doctor
/// keeps answering when the TIA tools themselves are broken.</summary>
public sealed class DoctorRunner
{
    private readonly string _hostVersion;
    private readonly IReadOnlyList<IDiagnosticCheck> _checks;

    public DoctorRunner(string hostVersion, IReadOnlyList<IDiagnosticCheck> checks)
    {
        _hostVersion = hostVersion;
        _checks = checks;
    }

    public DoctorReport Run()
    {
        var results = new List<DiagnosticCheckResult>(_checks.Count);
        foreach (var check in _checks)
        {
            try
            {
                results.Add(check.Run());
            }
            catch (Exception ex)
            {
                results.Add(DiagnosticCheckResult.Create(
                    check.Id,
                    DiagnosticStatus.Fail,
                    $"check crashed: {ex.Message}",
                    new[] { $"exception: {ex.GetType().FullName}" }));
            }
        }

        var status = Worst(results.Select(r => r.Status));
        return new DoctorReport(status, DateTimeOffset.UtcNow, _hostVersion, results);
    }

    /// <summary>Worst-of aggregation. Info deliberately does NOT rank above Ok — an Info check
    /// ("no TIA Portal running") is context, not a readiness problem; only Warn/Fail degrade.</summary>
    internal static DiagnosticStatus Worst(IEnumerable<DiagnosticStatus> statuses)
        => statuses.Aggregate(DiagnosticStatus.Ok, (worst, s) => Rank(s) > Rank(worst) ? s : worst);

    private static int Rank(DiagnosticStatus status)
        => status switch
        {
            DiagnosticStatus.Fail => 3,
            DiagnosticStatus.Warn => 2,
            _ => 0
        };
}
