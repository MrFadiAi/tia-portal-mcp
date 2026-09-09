using TiaMcpServer.Diagnostics;
using TiaMcpServer.Diagnostics.Checks;
using Xunit;

namespace TiaMcpServer.Tests;

public class DoctorTests
{
    // --------------------------------------------------------------------- fakes

    private sealed class FakeRegistry : IRegistryReader
    {
        public Dictionary<string, string> Strings { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, long> Ints { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, IReadOnlyList<string>> SubKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, IReadOnlyList<KeyValuePair<string, string>>> KeyValues { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public string? GetLocalMachineString(string subKey, string valueName)
            => Strings.TryGetValue(subKey + "|" + valueName, out var v) ? v : null;

        public long? GetLocalMachineInt64(string subKey, string valueName)
            => Ints.TryGetValue(subKey + "|" + valueName, out var v) ? v : null;

        public IReadOnlyList<string> GetLocalMachineSubKeyNames(string subKey)
            => SubKeys.TryGetValue(subKey, out var names) ? names : Array.Empty<string>();

        public IReadOnlyList<KeyValuePair<string, string>> GetLocalMachineStringValues(string subKey)
            => KeyValues.TryGetValue(subKey, out var values) ? values : Array.Empty<KeyValuePair<string, string>>();
    }

    private sealed class FakeFiles : IFileSystemProbe
    {
        public HashSet<string> ExistingFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, DateTimeOffset> Mtimes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Versions { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Hashes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, IReadOnlyList<string>> Found { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool FileExists(string path) => ExistingFiles.Contains(path);

        public bool DirectoryExists(string path) => ExistingFiles.Contains(path + "\\.");

        public DateTimeOffset? GetLastWriteTimeUtc(string path)
            => Mtimes.TryGetValue(path, out var t) ? t : null;

        public string? GetFileVersion(string path)
            => Versions.TryGetValue(path, out var v) ? v : null;

        public string? ComputeSha256Base64(string path)
            => Hashes.TryGetValue(path, out var h) ? h : null;

        public IReadOnlyList<string> FindFiles(string root, string searchPattern)
            => Found.TryGetValue(root + "|" + searchPattern, out var files) ? files : Array.Empty<string>();
    }

    private sealed class FakeEnv : IEnvironmentInfo
    {
        public bool IsWindows { get; set; } = true;
        public string OsVersionDescription { get; set; } = "Microsoft Windows NT 10.0.26200.0";
        public string RuntimeVersion { get; set; } = "8.0.12";
        public string HostVersion { get; set; } = "1.2.3.0";
        public string? HostExePath { get; set; } = @"D:\app\TiaMcpServer.exe";
        public string BaseDirectory { get; set; } = @"D:\app";
    }

    private sealed class FakeProcesses(params string[] names) : IProcessLister
    {
        public IReadOnlyList<string> Names { get; } = names;

        public IReadOnlyList<string> GetProcessNames() => Names;
    }

    private sealed class FakeIdentity : IUserIdentity
    {
        public string? UserName { get; set; } = @"MACHINE\engineer";

        public IReadOnlyList<string>? Groups { get; set; } = Array.Empty<string>();

        public IReadOnlyList<string>? TryGetGroupNames() => Groups;
    }

    private sealed class FakeCheck(string id, DiagnosticStatus status) : IDiagnosticCheck
    {
        public string Id { get; } = id;

        public DiagnosticCheckResult Run()
            => DiagnosticCheckResult.Create(Id, status, $"{Id} summary", new[] { $"{Id} evidence" });
    }

    private sealed class ThrowingCheck(string id) : IDiagnosticCheck
    {
        public string Id { get; } = id;

        public DiagnosticCheckResult Run() => throw new InvalidOperationException("boom");
    }

    // ------------------------------------------------------------------ whitelist verdicts

    [Fact]
    public void Whitelist_MatchingPathAndHash_IsOk()
    {
        var entries = new[]
        {
            new WhitelistCheck.WhitelistEntry(@"D:\app\openness-worker\TiaMcpServer.OpennessWorker.exe", "OLDHASH"),
            new WhitelistCheck.WhitelistEntry(@"D:\app\openness-worker\TiaMcpServer.OpennessWorker.exe", "CURRENTHASH")
        };

        var verdict = WhitelistCheck.EvaluateEntries(
            entries, @"D:\app\openness-worker\TiaMcpServer.OpennessWorker.exe", "CURRENTHASH");

        Assert.Equal(DiagnosticStatus.Ok, verdict.Status);
    }

    [Fact]
    public void Whitelist_PathMatchesButHashDiffers_IsFail()
    {
        var entries = new[]
        {
            new WhitelistCheck.WhitelistEntry(@"D:\app\openness-worker\TiaMcpServer.OpennessWorker.exe", "NEWHASH")
        };

        var verdict = WhitelistCheck.EvaluateEntries(
            entries, @"D:\app\openness-worker\TiaMcpServer.OpennessWorker.exe", "DIFFERENT");

        Assert.Equal(DiagnosticStatus.Fail, verdict.Status);
        Assert.Contains("whitelist-tia-openness.bat", verdict.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Whitelist_NoEntries_IsWarn()
    {
        var verdict = WhitelistCheck.EvaluateEntries(
            Array.Empty<WhitelistCheck.WhitelistEntry>(), @"D:\app\TiaMcpServer.exe", "HASH");

        Assert.Equal(DiagnosticStatus.Warn, verdict.Status);
    }

    [Fact]
    public void Whitelist_EntriesOnlyForOtherPaths_IsWarn()
    {
        var entries = new[]
        {
            new WhitelistCheck.WhitelistEntry(@"C:\elsewhere\TiaMcpServer.exe", "HASH")
        };

        var verdict = WhitelistCheck.EvaluateEntries(entries, @"D:\app\TiaMcpServer.exe", "HASH");

        Assert.Equal(DiagnosticStatus.Warn, verdict.Status);
    }

    [Theory]
    [InlineData("TiaMcpServer.exe", true)]
    [InlineData("tiamcpserver.exe", true)]
    [InlineData("TiaMcpServer.OpennessWorker.exe", true)]
    [InlineData("TiaMcpServer.OpennessWorker.Legacy.exe", true)]
    [InlineData("TiaMcpServer.OpennessWorker.V16.exe", true)]
    [InlineData("TiaMcpServer.OpennessWorker.V18.exe", false)]
    [InlineData("OtherTool.exe", false)]
    [InlineData("xTiaMcpServer.exe", false)]
    public void Whitelist_MonitoredExeName_FiltersCorrectly(string fileName, bool expected)
        => Assert.Equal(expected, WhitelistCheck.IsMonitoredExeName(fileName));

    [Theory]
    [InlineData(@"D:\repo\obj\Debug\TiaMcpServer.exe", true)]
    [InlineData(@"D:\repo\TiaMcpServer\verify\TiaMcpServer.exe", true)]
    [InlineData(@"D:\repo\objects\x.exe", false)] // "objects" is NOT the obj dir
    [InlineData(@"D:\repo\bin\Debug\TiaMcpServer.exe", false)]
    public void Whitelist_ExcludesObjAndVerifyPaths(string fullPath, bool expectedExcluded)
        => Assert.Equal(expectedExcluded, WhitelistCheck.IsExcludedBuildPath(fullPath));

    [Theory]
    [InlineData("16.0", true)]
    [InlineData("18.0", true)]
    [InlineData("21.0", true)]
    [InlineData("Whitelist", false)]
    [InlineData("18", false)]
    [InlineData("21.0.1", false)]
    public void Whitelist_OpennessVersionKey_AcceptsMajorMinorOnly(string keyName, bool expected)
        => Assert.Equal(expected, WhitelistCheck.IsOpennessVersionKey(keyName));

    [Theory]
    [InlineData("Entry", true)]
    [InlineData("Entry (1)", true)]
    [InlineData("Entry (12)", true)]
    [InlineData("EntryX", false)]
    [InlineData("Entry (1", false)]
    [InlineData("Entry 1", false)]
    public void Whitelist_EntryKeyName_AcceptsEntryAndNumberedEntries(string keyName, bool expected)
        => Assert.Equal(expected, WhitelistCheck.IsEntryKeyName(keyName));

    [Fact]
    public void Whitelist_Run_OneVersionMismatched_AggregatesToFail()
    {
        const string exe = @"D:\repo\TiaMcpServer.OpennessWorker\bin\Debug\net48\TiaMcpServer.OpennessWorker.exe";
        const string exeKey18 = @"SOFTWARE\Siemens\Automation\Openness\18.0\Whitelist\TiaMcpServer.OpennessWorker.exe";
        const string exeKey21 = @"SOFTWARE\Siemens\Automation\Openness\21.0\Whitelist\TiaMcpServer.OpennessWorker.exe";

        var registry = new FakeRegistry();
        registry.SubKeys[WhitelistCheck.OpennessRootKey] = new[] { "18.0", "21.0" };
        registry.SubKeys[exeKey18] = new[] { "Entry" };
        registry.SubKeys[exeKey21] = new[] { "Entry" };
        registry.KeyValues[exeKey18 + @"\Entry"] = new[]
        {
            new KeyValuePair<string, string>("Path", exe),
            new KeyValuePair<string, string>("FileHash", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")
        };
        registry.KeyValues[exeKey21 + @"\Entry"] = new[]
        {
            new KeyValuePair<string, string>("Path", exe),
            new KeyValuePair<string, string>("FileHash", "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB=")
        };

        var files = new FakeFiles();
        files.Found[@"D:\repo|*.exe"] = new[] { exe, @"D:\repo\obj\Debug\TiaMcpServer.exe" };
        files.Hashes[exe] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

        var result = new WhitelistCheck(registry, files, @"D:\repo", @"D:\app").Run();

        Assert.Equal(DiagnosticStatus.Fail, result.Status);
        Assert.Contains(result.Evidence, e => e.Equals("exe: " + exe, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Evidence, e => e.StartsWith("hash(current): ", StringComparison.Ordinal) && e.Length == "hash(current): ".Length + 12);
        Assert.Contains(result.Evidence, e => e.StartsWith("v18.0: ", StringComparison.Ordinal));
        Assert.Contains(result.Evidence, e => e.StartsWith("v21.0: ", StringComparison.Ordinal));
    }

    [Fact]
    public void Whitelist_Run_NoExes_Warns()
    {
        var registry = new FakeRegistry();
        registry.SubKeys[WhitelistCheck.OpennessRootKey] = new[] { "21.0" };
        var files = new FakeFiles();

        var result = new WhitelistCheck(registry, files, @"D:\repo", @"D:\app").Run();

        Assert.Equal(DiagnosticStatus.Warn, result.Status);
    }

    [Fact]
    public void Whitelist_Run_NoVersions_Warns()
    {
        var registry = new FakeRegistry();
        var files = new FakeFiles();

        var result = new WhitelistCheck(registry, files, @"D:\repo", @"D:\app").Run();

        Assert.Equal(DiagnosticStatus.Warn, result.Status);
    }

    [Fact]
    public void Whitelist_Run_AllMatched_IsOk()
    {
        const string exe = @"D:\repo\TiaMcpServer\bin\Debug\net8.0\TiaMcpServer.exe";
        const string exeKey = @"SOFTWARE\Siemens\Automation\Openness\21.0\Whitelist\TiaMcpServer.exe";

        var registry = new FakeRegistry();
        registry.SubKeys[WhitelistCheck.OpennessRootKey] = new[] { "21.0" };
        registry.SubKeys[exeKey] = new[] { "Entry" };
        registry.KeyValues[exeKey + @"\Entry"] = new[]
        {
            new KeyValuePair<string, string>("Path", exe),
            new KeyValuePair<string, string>("FileHash", "MATCH")
        };

        var files = new FakeFiles();
        files.Found[@"D:\repo|*.exe"] = new[] { exe };
        files.Hashes[exe] = "MATCH";

        var result = new WhitelistCheck(registry, files, @"D:\repo", null).Run();

        Assert.Equal(DiagnosticStatus.Ok, result.Status);
    }

    // ----------------------------------------------------------- worker-binary staleness

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WorkerStale_MinutesOfSkew_IsFresh_NormalBuildOrdering()
    {
        // dotnet builds the workers first and the host last, so a worker a few minutes older
        // than the host is a normal fresh launch (was a false-positive WARN before the threshold).
        Assert.False(WorkerBinariesCheck.EvaluateStaleness(T0.AddMinutes(-3), T0).Stale);
        Assert.False(WorkerBinariesCheck.EvaluateStaleness(T0.AddMinutes(-5), T0).Stale);
        Assert.False(WorkerBinariesCheck.EvaluateStaleness(T0, T0).Stale);
        Assert.False(WorkerBinariesCheck.EvaluateStaleness(T0.AddMinutes(5), T0).Stale);
    }

    [Fact]
    public void WorkerStale_SkewBeyondThreshold_IsStale()
    {
        var (stale, reason) = WorkerBinariesCheck.EvaluateStaleness(T0.AddHours(-2), T0);

        Assert.True(stale);
        Assert.NotNull(reason);
        Assert.Contains("rebuild", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkerStale_ThresholdBoundary_TenMinutesExactly_IsFresh()
    {
        var (stale, _) = WorkerBinariesCheck.EvaluateStaleness(T0.AddMinutes(-10), T0);

        Assert.False(stale);
    }

    [Fact]
    public void WorkerStale_UnknownTimes_AreNotJudged()
    {
        Assert.False(WorkerBinariesCheck.EvaluateStaleness(null, T0).Stale);
        Assert.False(WorkerBinariesCheck.EvaluateStaleness(T0, null).Stale);
        Assert.False(WorkerBinariesCheck.EvaluateStaleness(null, null).Stale);
    }

    [Fact]
    public void WorkerBinaries_Run_MissingWorkers_Warn()
    {
        var files = new FakeFiles();
        files.ExistingFiles.Add(@"D:\app\TiaMcpServer.exe");
        files.Mtimes[@"D:\app\TiaMcpServer.exe"] = T0;

        var result = new WorkerBinariesCheck(files, @"D:\repo", @"D:\app\TiaMcpServer.exe").Run();

        Assert.Equal(DiagnosticStatus.Warn, result.Status);
        Assert.Contains(result.Evidence, e => e.Contains("NOT FOUND", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkerBinaries_Run_StalePackagedWorker_Warns()
    {
        var files = new FakeFiles();
        files.ExistingFiles.Add(@"D:\app\TiaMcpServer.exe");
        files.Mtimes[@"D:\app\TiaMcpServer.exe"] = T0;
        foreach (var (exeName, _) in WorkerBinariesCheck.Workers)
        {
            var path = Path.Combine(@"D:\app\openness-worker", exeName);
            files.ExistingFiles.Add(path);
            files.Mtimes[path] = T0.AddHours(-1); // all older than the host
        }

        var result = new WorkerBinariesCheck(files, @"D:\repo", @"D:\app\TiaMcpServer.exe").Run();

        Assert.Equal(DiagnosticStatus.Warn, result.Status);
        Assert.Contains("stale", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkerBinaries_Run_AllPresentAndCurrent_Ok()
    {
        var files = new FakeFiles();
        files.ExistingFiles.Add(@"D:\app\TiaMcpServer.exe");
        files.Mtimes[@"D:\app\TiaMcpServer.exe"] = T0;
        foreach (var (exeName, _) in WorkerBinariesCheck.Workers)
        {
            var path = Path.Combine(@"D:\app\openness-worker", exeName);
            files.ExistingFiles.Add(path);
            files.Mtimes[path] = T0.AddMinutes(1);
        }

        var result = new WorkerBinariesCheck(files, @"D:\repo", @"D:\app\TiaMcpServer.exe").Run();

        Assert.Equal(DiagnosticStatus.Ok, result.Status);
    }

    // ---------------------------------------------------------------------- host/worker versions

    [Fact]
    public void HostWorker_SameMajor_IsCompatible()
    {
        var (compatible, _) = HostWorkerVersionCheck.EvaluateMatch("1.2.3.0", "1.9.0.0");

        Assert.True(compatible);
    }

    [Fact]
    public void HostWorker_DifferentMajor_IsNotCompatible()
    {
        var (compatible, detail) = HostWorkerVersionCheck.EvaluateMatch("1.2.3.0", "2.0.0.0");

        Assert.False(compatible);
        Assert.Contains("rebuild", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HostWorker_UnparsableVersion_IsNotCompatible()
    {
        Assert.False(HostWorkerVersionCheck.EvaluateMatch("1.2.3.0", "garbage").Compatible);
        Assert.False(HostWorkerVersionCheck.EvaluateMatch("garbage", "1.2.3.0").Compatible);
        Assert.False(HostWorkerVersionCheck.EvaluateMatch("1.2.3.0", null).Compatible);
    }

    // ---------------------------------------------------------------------------- other checks

    [Fact]
    public void Group_MembershipMatchesOnNamePortion()
    {
        Assert.True(OpennessGroupCheck.IsOpennessGroupMember(new[]
        {
            @"MACHINE\Users", @"MACHINE\Siemens TIA Openness"
        }));
        Assert.True(OpennessGroupCheck.IsOpennessGroupMember(new[] { "siemens tia openness" }));
        Assert.False(OpennessGroupCheck.IsOpennessGroupMember(new[] { @"MACHINE\Users" }));
        Assert.False(OpennessGroupCheck.IsOpennessGroupMember(Array.Empty<string>()));
    }

    [Fact]
    public void Process_Count_FiltersByTiaPrefix()
    {
        var names = new[] { "explorer", "Siemens.Automation.Portal", "SIEMENS.AUTOMATION.PORTAL.EXE", "chrome" };

        Assert.Equal(2, TiaPortalProcessCheck.CountTiaProcesses(names));
    }

    [Fact]
    public void Install_Evaluate_Statuses()
    {
        Assert.Equal(DiagnosticStatus.Info, TiaPortalInstallationCheck.Evaluate(false, null, 18).Status);
        Assert.Equal(
            DiagnosticStatus.Warn,
            TiaPortalInstallationCheck.Evaluate(true, null, 18).Status);
        Assert.Equal(
            DiagnosticStatus.Ok,
            TiaPortalInstallationCheck.Evaluate(true, @"C:\Siemens\Portal V18\PublicAPI\V18", 18).Status);
    }

    [Fact]
    public void Install_Probe_UsesRegistryAndAssemblies()
    {
        var registry = new FakeRegistry();
        registry.Strings[@"SOFTWARE\Siemens\Automation\InstalledApps\Totally Integrated Automation Portal V16|INSTALLPATH"] =
            @"C:\Siemens\Portal V16";

        var files = new FakeFiles();
        files.Found[@"C:\Siemens\Portal V16\PublicAPI\V16\net48|Siemens.Engineering*.dll"] = Array.Empty<string>();
        files.Found[@"C:\Siemens\Portal V16\PublicAPI\V16|Siemens.Engineering*.dll"] =
            new[] { @"C:\Siemens\Portal V16\PublicAPI\V16\Siemens.Engineering.dll" };

        var verdict = TiaPortalInstallationCheck.ProbeVersion(registry, files, 16, @"C:\TIAStd");

        Assert.Equal(DiagnosticStatus.Ok, verdict.Status);
        Assert.Contains("PublicAPI", verdict.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Install_Probe_StandardLocation_FindsAssemblies_WithoutRegistry()
    {
        // This machine's reality: no INSTALLPATH registry layout at all — the assemblies are
        // found at the standard install location, which alone proves the installation.
        var registry = new FakeRegistry();
        var files = new FakeFiles();
        const string net48 = @"C:\TIAStd\Portal V21\PublicAPI\V21\net48";
        files.Found[net48 + "|Siemens.Engineering*.dll"] =
            new[] { net48 + @"\Siemens.Engineering.dll" };

        var verdict = TiaPortalInstallationCheck.ProbeVersion(registry, files, 21, @"C:\TIAStd");

        Assert.Equal(DiagnosticStatus.Ok, verdict.Status);
        Assert.Contains("net48", verdict.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Install_Probe_BundleEvidence_WithoutAssemblies_Warns()
    {
        var registry = new FakeRegistry();
        registry.SubKeys[TiaPortalInstallationCheck.BundlesKey] =
            new[] { "Siemens TIA Portal V16.0 Upd7", "WinCC Panel Images V16" };
        var files = new FakeFiles();

        var verdict = TiaPortalInstallationCheck.ProbeVersion(registry, files, 16, @"C:\TIAStd");

        Assert.Equal(DiagnosticStatus.Warn, verdict.Status);
    }

    [Fact]
    public void Install_Probe_NoEvidenceAnywhere_IsInfo()
    {
        var registry = new FakeRegistry();
        registry.SubKeys[TiaPortalInstallationCheck.BundlesKey] =
            new[] { "Siemens TIA Portal V18.0 Upd5" };
        var files = new FakeFiles();

        // V18 has bundle evidence -> Warn; V16/V21 have nothing -> Info.
        Assert.Equal(DiagnosticStatus.Info, TiaPortalInstallationCheck.ProbeVersion(registry, files, 16, @"C:\TIAStd").Status);
        Assert.Equal(DiagnosticStatus.Info, TiaPortalInstallationCheck.ProbeVersion(registry, files, 21, @"C:\TIAStd").Status);
    }

    [Theory]
    [InlineData("Siemens TIA Portal V16.0 Upd7", 16, true)]
    [InlineData("TIA Portal STEP 7 V21 - WinCC V21", 21, true)]
    [InlineData("TIA Portal V18 WinCC BCA Ed", 18, true)]
    [InlineData("SIMATIC WinCC Panel Images for TIA Portal V16", 16, true)]
    [InlineData("Siemens TIA Portal V16.0 Upd7", 18, false)]
    [InlineData("Something V160", 16, false)]
    [InlineData("Totally different product", 16, false)]
    public void Install_BundleName_MatchesPortalAndVersion(string bundleName, int version, bool expected)
        => Assert.Equal(expected, TiaPortalInstallationCheck.IsBundleForVersion(bundleName, version));

    // ---------------------------------------------------------------------- runner + renderers

    [Fact]
    public void Runner_WorstStatusAggregates()
    {
        var report = new DoctorRunner("1.0.0", new IDiagnosticCheck[]
        {
            new FakeCheck("a", DiagnosticStatus.Ok),
            new FakeCheck("b", DiagnosticStatus.Warn),
            new FakeCheck("c", DiagnosticStatus.Ok)
        }).Run();

        Assert.Equal(DiagnosticStatus.Warn, report.Status);
        Assert.Equal(3, report.Checks.Count);
        var (ok, info, warn, fail) = DoctorCounts.Of(report);
        Assert.Equal(2, ok);
        Assert.Equal(0, info);
        Assert.Equal(1, warn);
        Assert.Equal(0, fail);
    }

    [Fact]
    public void Runner_ThrowingCheck_BecomesFailAndOthersStillRun()
    {
        var report = new DoctorRunner("1.0.0", new IDiagnosticCheck[]
        {
            new ThrowingCheck("exploder"),
            new FakeCheck("b", DiagnosticStatus.Ok)
        }).Run();

        Assert.Equal(DiagnosticStatus.Fail, report.Status);
        Assert.Equal(2, report.Checks.Count);
        var exploded = Assert.Single(report.Checks, c => c.Id == "exploder");
        Assert.Equal(DiagnosticStatus.Fail, exploded.Status);
        Assert.Contains("boom", exploded.Summary, StringComparison.Ordinal);
        Assert.Contains(report.Checks, c => c.Id == "b" && c.Status == DiagnosticStatus.Ok);
    }

    [Fact]
    public void Runner_AllOk_IsOk()
    {
        var report = new DoctorRunner("1.0.0", new IDiagnosticCheck[]
        {
            new FakeCheck("a", DiagnosticStatus.Ok),
            new FakeCheck("b", DiagnosticStatus.Info)
        }).Run();

        Assert.Equal(DiagnosticStatus.Ok, report.Status);
    }

    [Fact]
    public void TextRenderer_ShowsCheckLinesAndSummaryCounts()
    {
        var report = new DoctorReport(
            DiagnosticStatus.Warn,
            new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero),
            "1.2.3",
            new[]
            {
                DiagnosticCheckResult.Create("os", DiagnosticStatus.Ok, "Running on Windows.", new[] { "os: win" }),
                DiagnosticCheckResult.Create("tia-processes", DiagnosticStatus.Info, "No TIA Portal running."),
                DiagnosticCheckResult.Create("whitelist", DiagnosticStatus.Warn, "Some exes have no entry.")
            });

        var text = DoctorTextRenderer.Render(report);

        Assert.Contains("[OK]   os", text, StringComparison.Ordinal);
        Assert.Contains("[INFO] tia-processes", text, StringComparison.Ordinal);
        Assert.Contains("[WARN] whitelist", text, StringComparison.Ordinal);
        Assert.Contains("- os: win", text, StringComparison.Ordinal);
        Assert.Contains("Summary: 1 ok, 1 info, 1 warn, 0 fail", text, StringComparison.Ordinal);
        Assert.Contains("Ready with warnings", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TextRenderer_AnyFail_RendersNotReadyVerdict()
    {
        var report = new DoctorReport(
            DiagnosticStatus.Fail,
            DateTimeOffset.UtcNow,
            "1.2.3",
            new[] { DiagnosticCheckResult.Create("whitelist", DiagnosticStatus.Fail, "hash mismatch.") });

        var text = DoctorTextRenderer.Render(report);

        Assert.Contains("[FAIL] whitelist", text, StringComparison.Ordinal);
        Assert.Contains("Summary: 0 ok, 0 info, 0 warn, 1 fail", text, StringComparison.Ordinal);
        Assert.Contains("NOT READY", text, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonRenderer_RoundTripsTheReport()
    {
        var original = new DoctorReport(
            DiagnosticStatus.Warn,
            new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero),
            "1.2.3",
            new[]
            {
                DiagnosticCheckResult.Create("os", DiagnosticStatus.Ok, "Running on Windows.", new[] { "os: win" }),
                DiagnosticCheckResult.Create(
                    "whitelist",
                    DiagnosticStatus.Fail,
                    "Rebuilt exe not re-approved.",
                    new[] { "exe: D:\\app\\worker.exe", "hash(current): AAAABBBBCCCC" })
            });

        var parsed = DoctorJsonRenderer.Parse(DoctorJsonRenderer.Render(original));

        Assert.NotNull(parsed);
        Assert.Equal(original.Status, parsed.Status);
        Assert.Equal(original.TimestampUtc, parsed.TimestampUtc);
        Assert.Equal(original.HostVersion, parsed.HostVersion);
        Assert.Equal(original.Checks.Select(c => c.Id), parsed.Checks.Select(c => c.Id));
        Assert.Equal(original.Checks.Select(c => c.Status), parsed.Checks.Select(c => c.Status));
        Assert.Equal(original.Checks.Select(c => c.Summary), parsed.Checks.Select(c => c.Summary));
        Assert.Equal(
            original.Checks.SelectMany(c => c.Evidence),
            parsed.Checks.SelectMany(c => c.Evidence));
        Assert.Contains("\"status\": \"Warn\"", DoctorJsonRenderer.Render(original), StringComparison.Ordinal);
    }

    [Fact]
    public void FindRepoRoot_WalksUpToTheSlnMarker()
    {
        var files = new FakeFiles();
        files.ExistingFiles.Add(@"D:\src\TiaMcpServer.sln");

        var root = DoctorFactory.FindRepoRoot(@"D:\src\TiaMcpServer\bin\Debug\net8.0", files);

        Assert.Equal(@"D:\src", root);
    }

    [Fact]
    public void FindRepoRoot_NoMarker_ReturnsNull()
    {
        var files = new FakeFiles();
        files.ExistingFiles.Add(@"C:\unrelated.txt");

        var root = DoctorFactory.FindRepoRoot(@"D:\nowhere", files);

        Assert.Null(root);
    }
}
