namespace TiaMcpServer.Diagnostics.Checks;

/// <summary>Host vs worker assembly versions — a mismatch means a half-updated install (one exe
/// rebuilt, the other left behind). Warn, not Fail: mixed versions often still work.</summary>
public sealed class HostWorkerVersionCheck(
    IEnvironmentInfo env,
    IFileSystemProbe fileSystem,
    string repoRoot) : IDiagnosticCheck
{
    public string Id => "host-worker-version";

    public DiagnosticCheckResult Run()
    {
        var hostVersion = env.HostVersion;
        var evidence = new List<string> { $"host: {hostVersion}" };

        var hostExePath = env.HostExePath ?? Path.Combine(env.BaseDirectory, "TiaMcpServer.exe");
        var worst = DiagnosticStatus.Ok;
        foreach (var (exeName, projectDir) in WorkerBinariesCheck.Workers)
        {
            var path = WorkerBinariesCheck.FindWorkerPath(fileSystem, repoRoot, hostExePath, exeName, projectDir);
            if (path is null)
            {
                evidence.Add($"{exeName}: (not found — skipped)");
                continue;
            }

            var workerVersion = fileSystem.GetFileVersion(path);
            var (compatible, detail) = EvaluateMatch(hostVersion, workerVersion);
            evidence.Add($"{exeName}: {workerVersion ?? "(no version)"} — {detail}");
            if (!compatible)
            {
                worst = DiagnosticStatus.Warn;
            }
        }

        return worst == DiagnosticStatus.Warn
            ? DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Warn,
                "Host and worker versions do not match — rebuild the WHOLE solution (a stale worker from an older build is still on disk).",
                evidence)
            : DiagnosticCheckResult.Create(
                Id,
                worst,
                "Host and worker versions agree.",
                evidence);
    }

    /// <summary>Pure: compare major versions (the part that matters for protocol compatibility);
    /// unparsable versions are a Warn-grade mismatch, never a crash.</summary>
    internal static (bool Compatible, string Detail) EvaluateMatch(string? hostVersion, string? workerVersion)
    {
        if (!TryParseMajor(hostVersion, out var hostMajor))
        {
            return (false, $"host version '{hostVersion ?? "(null)"}' could not be parsed");
        }

        if (!TryParseMajor(workerVersion, out var workerMajor))
        {
            return (false, $"worker version '{workerVersion ?? "(null)"}' could not be parsed");
        }

        return hostMajor == workerMajor
            ? (true, $"major versions agree ({hostMajor})")
            : (false, $"major version differs (host {hostMajor} vs worker {workerMajor}) — rebuild the whole solution");
    }

    private static bool TryParseMajor(string? version, out int major)
    {
        major = 0;
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var span = version.AsSpan();
        var dot = span.IndexOf('.');
        var majorSpan = dot < 0 ? span : span[..dot];
        return int.TryParse(majorSpan, out major) && major > 0;
    }
}
