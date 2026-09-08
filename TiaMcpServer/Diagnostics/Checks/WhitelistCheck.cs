using System.Text.RegularExpressions;

namespace TiaMcpServer.Diagnostics.Checks;

/// <summary>OUR killer check: TIA's Openness "Business access" whitelist must hold an Entry whose
/// Path matches the exe AND whose FileHash matches the exe's CURRENT SHA-256 (base64) — the exact
/// rule TIA applies at attach time (see scripts/tia-openness-whitelist.ps1). A rebuilt exe has a
/// new hash, so this is the #1 "worker suddenly prompts/fails" cause. Host-side only: registry +
/// file hashes, no TIA interaction.</summary>
public sealed partial class WhitelistCheck(
    IRegistryReader registry,
    IFileSystemProbe fileSystem,
    string repoRoot,
    string? hostDirectory) : IDiagnosticCheck
{
    public const string OpennessRootKey = @"SOFTWARE\Siemens\Automation\Openness";

    private static readonly Regex ExeName = new(
        @"^TiaMcpServer(\.OpennessWorker(\.(Legacy|V16))?)?\.exe$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string Id => "whitelist";

    public DiagnosticCheckResult Run()
    {
        var versions = registry
            .GetLocalMachineSubKeyNames(OpennessRootKey)
            .Where(IsOpennessVersionKey)
            .Order()
            .ToList();

        var exes = DiscoverExes(fileSystem, repoRoot, hostDirectory);

        var evidence = new List<string>
        {
            $"repo root: {repoRoot}",
            $"tia versions: {string.Join(", ", versions)}",
            $"exes: {exes.Count}"
        };

        if (versions.Count == 0)
        {
            return DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Warn,
                "No TIA Openness versions found in the registry — has TIA Portal ever run on this machine?",
                evidence);
        }

        if (exes.Count == 0)
        {
            return DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Warn,
                "No TiaMcpServer worker/host exes found — build the solution first (dotnet build).",
                evidence);
        }

        var statuses = new List<DiagnosticStatus>();
        foreach (var exe in exes)
        {
            var currentHash = fileSystem.ComputeSha256Base64(exe);
            evidence.Add($"exe: {exe}");
            evidence.Add($"hash(current): {Truncate(currentHash)}");

            if (currentHash is null)
            {
                statuses.Add(DiagnosticStatus.Warn);
                evidence.Add("hash(current): UNREADABLE");
                continue;
            }

            var exeStatus = DiagnosticStatus.Ok;
            var exeName = Path.GetFileName(exe);
            foreach (var version in versions)
            {
                var entries = ReadEntries(registry, version, exeName);
                var verdict = EvaluateEntries(entries, exe, currentHash);
                if (Rank(verdict.Status) > Rank(exeStatus))
                {
                    exeStatus = verdict.Status;
                }

                evidence.Add($"v{version}: {verdict.Detail}");
            }

            statuses.Add(exeStatus);
        }

        var overall = statuses.Aggregate(DiagnosticStatus.Ok, (worst, s) => Rank(s) > Rank(worst) ? s : worst);
        var summary = overall switch
        {
            DiagnosticStatus.Fail =>
                "A whitelisted exe's hash no longer matches the registry Entry — a rebuilt binary was not re-approved. Run scripts\\whitelist-tia-openness.bat (Approve Always), then restart the app.",
            DiagnosticStatus.Warn =>
                "Some exes have no matching whitelist Entry — the first Openness attach for them will prompt.",
            _ => "All discovered exes are whitelisted with matching hashes (no Business-access prompt expected)."
        };

        return DiagnosticCheckResult.Create(Id, overall, summary, evidence);
    }

    internal sealed record WhitelistEntry(string Path, string FileHash);

    internal sealed record WhitelistVerdict(DiagnosticStatus Status, string Detail);

    /// <summary>Pure verdict for one (exe, version): an Entry with matching Path AND FileHash →
    /// Ok; Entries exist for this path but every hash differs → Fail (rebuild not re-approved);
    /// otherwise (no Entries, or only Entries for other paths) → Warn (will prompt).</summary>
    internal static WhitelistVerdict EvaluateEntries(IReadOnlyList<WhitelistEntry> entries, string exePath, string currentHash)
    {
        if (entries.Count == 0)
        {
            return new WhitelistVerdict(
                DiagnosticStatus.Warn,
                "no whitelist entry — first Openness attach will prompt");
        }

        var forThisPath = entries
            .Where(e => string.Equals(e.Path, exePath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (forThisPath.Count == 0)
        {
            return new WhitelistVerdict(
                DiagnosticStatus.Warn,
                $"entry exists only for other paths ({entries.Count} entries) — first attach from this path will prompt");
        }

        return forThisPath.Any(e => string.Equals(e.FileHash, currentHash, StringComparison.Ordinal))
            ? new WhitelistVerdict(DiagnosticStatus.Ok, $"approved (hash {Truncate(currentHash)})")
            : new WhitelistVerdict(
                DiagnosticStatus.Fail,
                $"hash mismatch (entry {Truncate(forThisPath[0].FileHash)} vs current {Truncate(currentHash)}) — " +
                "rebuilt exe not re-approved; run scripts\\whitelist-tia-openness.bat (Approve Always)");
    }

    /// <summary>Pure: the exe-discovery name filter (same regex as the whitelist ps1) — host,
    /// three workers, nothing else (V17-V20 share the Legacy worker; other spellings are noise).</summary>
    internal static bool IsMonitoredExeName(string fileName)
        => ExeName.IsMatch(fileName);

    /// <summary>Pure: skip build intermediates (obj/, verify/) — same exclusion as the ps1.</summary>
    internal static bool IsExcludedBuildPath(string fullPath)
        => fullPath.Split('/', '\\')
            .Any(segment => segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                            segment.Equals("verify", StringComparison.OrdinalIgnoreCase));

    /// <summary>Pure: version sub-keys under the Openness root look like "16.0", "18.0", "21.0".</summary>
    internal static bool IsOpennessVersionKey(string keyName)
        => Regex.IsMatch(keyName, @"^\d+\.\d+$");

    /// <summary>Pure: whitelist Entry sub-keys look like "Entry", "Entry (1)", "Entry (2)".</summary>
    internal static bool IsEntryKeyName(string keyName)
        => Regex.IsMatch(keyName, @"^Entry( \(\d+\))?$");

    /// <summary>Exe discovery shared with the ps1's rule: every monitored exe under the repo,
    /// plus the packaged openness-worker folder beside the deployed host. Deduped, sorted.</summary>
    internal static IReadOnlyList<string> DiscoverExes(IFileSystemProbe fileSystem, string repoRoot, string? hostDirectory)
    {
        var roots = new List<string> { repoRoot };
        if (!string.IsNullOrEmpty(hostDirectory))
        {
            roots.Add(Path.Combine(hostDirectory, "openness-worker"));
        }

        return roots
            .SelectMany(root => fileSystem.FindFiles(root, "*.exe"))
            .Where(path => !IsExcludedBuildPath(path) && IsMonitoredExeName(Path.GetFileName(path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<WhitelistEntry> ReadEntries(IRegistryReader registry, string version, string exeName)
    {
        var exeKey = $@"{OpennessRootKey}\{version}\Whitelist\{exeName}";
        var entries = new List<WhitelistEntry>();
        foreach (var entryName in registry.GetLocalMachineSubKeyNames(exeKey).Where(IsEntryKeyName))
        {
            var values = registry.GetLocalMachineStringValues($@"{exeKey}\{entryName}");
            var path = values.FirstOrDefault(kvp => kvp.Key.Equals("Path", StringComparison.OrdinalIgnoreCase)).Value;
            var hash = values.FirstOrDefault(kvp => kvp.Key.Equals("FileHash", StringComparison.OrdinalIgnoreCase)).Value;
            if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(hash))
            {
                entries.Add(new WhitelistEntry(path!, hash!));
            }
        }

        return entries;
    }

    private static string Truncate(string? value)
        => string.IsNullOrEmpty(value) ? "(null)" : value.Length <= 12 ? value : value[..12];

    private static int Rank(DiagnosticStatus status)
        => status switch
        {
            DiagnosticStatus.Ok => 0,
            DiagnosticStatus.Info => 1,
            DiagnosticStatus.Warn => 2,
            DiagnosticStatus.Fail => 3,
            _ => 0
        };
}
