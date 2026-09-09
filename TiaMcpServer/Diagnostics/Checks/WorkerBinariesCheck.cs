namespace TiaMcpServer.Diagnostics.Checks;

/// <summary>The three version-routed net48 worker exes must exist and must not be MUCH older than
/// the host exe — a stale worker means a build was copy-locked while the app ran (MSB302x), which is
/// exactly the "rebuilt but behavior unchanged" trap. Minutes of skew is normal (the build produces
/// workers first, host last); only skew beyond <see cref="StalenessThreshold"/> warns. Probes the
/// packaged openness-worker folder beside the host first (what the host launches when deployed),
/// then the repo bin dirs.</summary>
public sealed class WorkerBinariesCheck(
    IFileSystemProbe fileSystem,
    string repoRoot,
    string hostExePath) : IDiagnosticCheck
{
    /// <summary>(exe name, project dir) — mirrors OpennessWorkerClient.WorkerIdentityForVersion.</summary>
    internal static readonly (string ExeName, string ProjectDir)[] Workers =
    [
        ("TiaMcpServer.OpennessWorker.exe", "TiaMcpServer.OpennessWorker"),
        ("TiaMcpServer.OpennessWorker.Legacy.exe", "TiaMcpServer.OpennessWorker.Legacy"),
        ("TiaMcpServer.OpennessWorker.V16.exe", "TiaMcpServer.OpennessWorker.V16")
    ];

    public string Id => "worker-binaries";

    public DiagnosticCheckResult Run()
    {
        var evidence = new List<string> { $"host exe: {hostExePath}" };
        var hostMtime = fileSystem.GetLastWriteTimeUtc(hostExePath);
        evidence.Add($"host lastWrite: {hostMtime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(missing)"}");

        var worst = DiagnosticStatus.Ok;
        foreach (var (exeName, projectDir) in Workers)
        {
            var path = FindWorkerPath(fileSystem, repoRoot, hostExePath, exeName, projectDir);
            if (path is null)
            {
                worst = DiagnosticStatus.Warn;
                evidence.Add($"{exeName}: NOT FOUND — build the solution (dotnet build)");
                continue;
            }

            var mtime = fileSystem.GetLastWriteTimeUtc(path);
            var (stale, reason) = EvaluateStaleness(mtime, hostMtime);
            if (stale)
            {
                worst = DiagnosticStatus.Warn;
            }

            evidence.Add($"{exeName}: {path}");
            evidence.Add($"  lastWrite: {mtime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(unreadable)"}");
            if (reason is not null)
            {
                evidence.Add($"  {reason}");
            }
        }

        var summary = worst == DiagnosticStatus.Warn
            ? "One or more worker binaries are missing or stale — close the app, rebuild, re-run Approve Always, restart."
            : "All three worker binaries are present and current with the host exe.";

        return DiagnosticCheckResult.Create(Id, worst, summary, evidence);
    }

    /// <summary>Skew below this is normal single-build ordering, not staleness: a solution build
    /// (and the app's own launch build) compiles/copies the workers FIRST and links the host
    /// LAST, so a freshly built worker is always minutes older than the host. Real copy-lock
    /// staleness (MSB302x — worker on disk from days ago, host from just now) is hours/days.</summary>
    internal static readonly TimeSpan StalenessThreshold = TimeSpan.FromMinutes(10);

    /// <summary>Pure staleness verdict: a worker older than the host exe by more than
    /// <see cref="StalenessThreshold"/> is stale (a build copied the host but skipped the
    /// copy-locked worker). Minutes of skew is normal build ordering; unknown times are not
    /// judged (the missing-exe case is handled by the caller).</summary>
    internal static (bool Stale, string? Reason) EvaluateStaleness(DateTimeOffset? workerMtimeUtc, DateTimeOffset? hostMtimeUtc)
    {
        if (workerMtimeUtc is null || hostMtimeUtc is null)
        {
            return (false, null);
        }

        var skew = hostMtimeUtc.Value - workerMtimeUtc.Value;
        return skew > StalenessThreshold
            ? (true, $"STALE — older than the host exe by {(int)skew.TotalMinutes} min (threshold {StalenessThreshold.TotalMinutes} min); a build was copy-locked while the app ran. Close the app, rebuild, re-run Approve Always")
            : (false, null);
    }

    /// <summary>Candidate paths for one worker, in launch-priority order: packaged
    /// openness-worker folder beside the host, then repo Debug, then repo Release bin.</summary>
    internal static string? FindWorkerPath(
        IFileSystemProbe fileSystem,
        string repoRoot,
        string hostExePath,
        string exeName,
        string projectDir)
    {
        var hostDirectory = Path.GetDirectoryName(hostExePath);
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(hostDirectory))
        {
            candidates.Add(Path.Combine(hostDirectory, "openness-worker", exeName));
        }

        candidates.Add(Path.Combine(repoRoot, projectDir, "bin", "Debug", "net48", exeName));
        candidates.Add(Path.Combine(repoRoot, projectDir, "bin", "Release", "net48", exeName));

        return candidates.FirstOrDefault(fileSystem.FileExists);
    }
}
