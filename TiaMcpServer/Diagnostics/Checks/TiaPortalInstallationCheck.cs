using System.Globalization;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Diagnostics.Checks;

/// <summary>Per-version TIA Portal installation probe. Install evidence, most to least
/// authoritative: a registry INSTALLPATH (three known Siemens layouts), an _InstalledSW bundle
/// for that version, an Openness registry version key, or the standard install directory.
/// Assemblies are then searched under PublicAPI (net48 subfolder and flat) at both the probed
/// install path and the standard location. Not installed is Info, not a failure — a machine can
/// legitimately run only one version.</summary>
public sealed partial class TiaPortalInstallationCheck(
    IRegistryReader registry,
    IFileSystemProbe fileSystem,
    string? standardInstallBase = null) : IDiagnosticCheck
{
    public static readonly int[] Versions = [16, 18, 21];

    public const string BundlesKey = @"SOFTWARE\Siemens\Automation\_InstalledSW\Global\Bundles";

    public string Id => "tia-installations";

    public DiagnosticCheckResult Run()
    {
        var standardBase = standardInstallBase ?? DefaultStandardBase();
        var lines = new List<string>();
        var statuses = new List<DiagnosticStatus>();
        foreach (var version in Versions)
        {
            var verdict = ProbeVersion(registry, fileSystem, version, standardBase);
            statuses.Add(verdict.Status);
            lines.Add($"v{version}: {verdict.Detail}");
        }

        var overall = statuses.Aggregate(DiagnosticStatus.Ok, (worst, s) => Rank(s) > Rank(worst) ? s : worst);
        var summary = overall switch
        {
            DiagnosticStatus.Warn => "One or more installed TIA Portal versions are missing their Openness assemblies.",
            DiagnosticStatus.Info => "No TIA Portal V16/V18/V21 installation detected.",
            _ => $"TIA Portal installations look complete ({string.Join(", ", Versions.Select(v => $"V{v}"))} probed)."
        };

        return DiagnosticCheckResult.Create(Id, overall, summary, lines);
    }

    internal sealed record VersionVerdict(DiagnosticStatus Status, string Detail);

    /// <summary>Pure aggregation once the raw facts are known: assemblies found → Ok; install
    /// evidence without assemblies → Warn; no evidence at all → Info.</summary>
    internal static VersionVerdict Evaluate(bool installedEvidence, string? assembliesDir, int version)
    {
        if (!string.IsNullOrWhiteSpace(assembliesDir))
        {
            return new VersionVerdict(DiagnosticStatus.Ok, $"Openness assemblies: {assembliesDir}");
        }

        return installedEvidence
            ? new VersionVerdict(
                DiagnosticStatus.Warn,
                $"install evidence found, but no Siemens.Engineering*.dll under PublicAPI (V{version})")
            : new VersionVerdict(DiagnosticStatus.Info, "not installed");
    }

    /// <summary>Gather one version's facts through the injected interfaces (fake-able).</summary>
    internal static VersionVerdict ProbeVersion(
        IRegistryReader registry,
        IFileSystemProbe fileSystem,
        int version,
        string standardBase)
    {
        // Install evidence 1: registry INSTALLPATH (three known Siemens layouts, in probe order).
        var probes = new[]
        {
            ($@"SOFTWARE\Siemens\Automation\InstalledApps\Totally Integrated Automation Portal V{version}", "INSTALLPATH"),
            ($@"SOFTWARE\Siemens\Automation\_InstalledSoftware\TIAP\{version}.0", "Path"),
            ($@"SOFTWARE\Siemens\Automation\Portal\V{version}", "INSTALLPATH")
        };

        string? installPath = null;
        foreach (var (subKey, valueName) in probes)
        {
            var path = registry.GetLocalMachineString(subKey, valueName);
            if (!string.IsNullOrWhiteSpace(path))
            {
                installPath = path;
                break;
            }
        }

        // Install evidence 2: an _InstalledSW bundle whose key names the version (this is where
        // V18/V21 actually record themselves on machines where the three layouts above miss).
        var bundleEvidence = registry
            .GetLocalMachineSubKeyNames(BundlesKey)
            .Any(name => IsBundleForVersion(name, version));

        // Install evidence 3: TIA itself created an Openness registry key for that version.
        var opennessEvidence = registry
            .GetLocalMachineSubKeyNames(WhitelistCheck.OpennessRootKey)
            .Any(name => name.Split('.')[0] == version.ToString(CultureInfo.InvariantCulture));

        // Assembly candidates: probed install path first, then the standard location. Covers both
        // Siemens layouts — PublicAPI\V<n>\net48 (V21+) and PublicAPI\V<n> flat (V16/V18).
        var standardPortalDir = Path.Combine(standardBase, $"Portal V{version}");
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(installPath))
        {
            candidates.Add(Path.Combine(installPath, "PublicAPI", $"V{version}", "net48"));
            candidates.Add(Path.Combine(installPath, "PublicAPI", $"V{version}"));
        }

        candidates.Add(Path.Combine(standardPortalDir, "PublicAPI", $"V{version}", "net48"));
        candidates.Add(Path.Combine(standardPortalDir, "PublicAPI", $"V{version}"));

        var assembliesDir = candidates
            .FirstOrDefault(dir => fileSystem.FindFiles(dir, "Siemens.Engineering*.dll").Count > 0);

        var installedEvidence = installPath is not null || bundleEvidence || opennessEvidence ||
            fileSystem.DirectoryExists(standardPortalDir);

        return Evaluate(installedEvidence, assembliesDir, version);
    }

    /// <summary>Pure: bundle keys look like "TIA Portal STEP 7 V21 - WinCC V21" or
    /// "Siemens TIA Portal V16.0 Upd7" — name the portal and the version, but not V160.</summary>
    internal static bool IsBundleForVersion(string bundleKeyName, int version)
        => bundleKeyName.Contains("TIA Portal", StringComparison.OrdinalIgnoreCase) &&
           Regex.IsMatch(bundleKeyName, $"V{version}(?!\\d)");

    private static string DefaultStandardBase()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Siemens", "Automation");

    private static int Rank(DiagnosticStatus status)
        => status switch
        {
            DiagnosticStatus.Fail => 3,
            DiagnosticStatus.Warn => 2,
            _ => 0
        };
}
