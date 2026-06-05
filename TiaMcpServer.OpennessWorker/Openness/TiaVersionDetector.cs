using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class TiaVersionDetector
{
    private static readonly int[] SupportedVersions = { 21, 19, 18, 16 };

    public static List<TiaVersionInfo> DetectInstalledVersions()
    {
        var results = new List<TiaVersionInfo>();
        var foundVersions = new HashSet<int>();

        // Method 1: Registry/filesystem detection
        foreach (var version in SupportedVersions)
        {
            var info = DetectVersion(version);
            if (info is not null)
            {
                results.Add(info);
                foundVersions.Add(version);
            }
        }

        // Method 2: Running process detection — find TIA Portal processes and extract version
        foreach (var running in DetectFromRunningProcesses())
        {
            if (!foundVersions.Contains(running.MajorVersion))
            {
                results.Add(running);
                foundVersions.Add(running.MajorVersion);
            }
        }

        return results;
    }

    /// <summary>
    /// Detects TIA Portal versions from running processes.
    /// TIA Portal runs as Siemens.TIA.Portal.StartEx.exe or Siemens.TIA.Portal.exe
    /// with the version embedded in the installation path.
    /// </summary>
    private static List<TiaVersionInfo> DetectFromRunningProcesses()
    {
        var results = new List<TiaVersionInfo>();

        try
        {
            var regex = new Regex(@"Portal\s+V(\d+)", RegexOptions.IgnoreCase);

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    // TIA Portal main process names
                    var name = process.ProcessName;
                    if (!name.StartsWith("Siemens.TIA.Portal") && !name.StartsWith("Siemens.Simulation"))
                    {
                        continue;
                    }

                    var exePath = process.MainModule?.FileName;
                    if (string.IsNullOrEmpty(exePath))
                    {
                        continue;
                    }

                    var match = regex.Match(exePath);
                    if (!match.Success)
                    {
                        continue;
                    }

                    var majorVersion = int.Parse(match.Groups[1].Value);
                    if (!SupportedVersions.Contains(majorVersion))
                    {
                        continue;
                    }

                    // Extract install root from exe path (e.g., C:\Program Files\Siemens\Automation\Portal V21\Bin\...)
                    var binIndex = exePath!.IndexOf("\\Bin\\", System.StringComparison.OrdinalIgnoreCase);
                    var installRoot = binIndex > 0 ? exePath.Substring(0, binIndex) : Path.GetDirectoryName(exePath) ?? "";

                    var dllDirectory = ResolveDllDirectory(installRoot!, majorVersion);

                    results.Add(new TiaVersionInfo
                    {
                        MajorVersion = majorVersion,
                        DisplayName = $"TIA Portal V{majorVersion}",
                        InstallPath = installRoot,
                        DllDirectory = dllDirectory ?? "",
                        UsesSplitDlls = dllDirectory is not null &&
                                       File.Exists(Path.Combine(dllDirectory, "Siemens.Engineering.Base.dll"))
                    });
                }
                catch
                {
                    // Skip processes we can't access (e.g., system processes)
                }
            }
        }
        catch
        {
            // Ignore process enumeration failures
        }

        return results;
    }

    public static TiaVersionInfo? DetectInstalledVersion(int? preferredMajorVersion = null)
    {
        var all = DetectInstalledVersions();

        // If a preferred version is specified and installed, use it
        if (preferredMajorVersion.HasValue)
        {
            var preferred = all.FirstOrDefault(v => v.MajorVersion == preferredMajorVersion.Value);
            if (preferred is not null)
            {
                return preferred;
            }
        }

        return all.FirstOrDefault();
    }

    private static TiaVersionInfo? DetectVersion(int majorVersion)
    {
        var displayName = $"TIA Portal V{majorVersion}";
        string? dllDirectory = null;

        // Env var fallback: TiaPortalV{XX}Dir
        var envPath = Environment.GetEnvironmentVariable($"TiaPortalV{majorVersion}Dir");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            var candidate = ResolveDllDirectory(envPath.Trim().Trim('"'), majorVersion);
            if (candidate is not null && Directory.Exists(candidate))
            {
                dllDirectory = candidate;
            }
        }

        // Registry probes
        if (dllDirectory is null)
        {
            dllDirectory = ProbeRegistry(majorVersion);
        }

        // Hardcoded fallback — pass install root only, ResolveDllDirectory appends PublicAPI\...
        if (dllDirectory is null)
        {
            var fallback = $"C:\\Program Files\\Siemens\\Automation\\Portal V{majorVersion}";
            var candidate = ResolveDllDirectory(fallback, majorVersion);
            if (candidate is not null && Directory.Exists(candidate))
            {
                dllDirectory = candidate;
            }
        }

        if (dllDirectory is null)
        {
            return null;
        }

        var usesSplitDlls = File.Exists(Path.Combine(dllDirectory, "Siemens.Engineering.Base.dll"));

        return new TiaVersionInfo
        {
            MajorVersion = majorVersion,
            DisplayName = displayName,
            InstallPath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(dllDirectory))),
            DllDirectory = dllDirectory,
            UsesSplitDlls = usesSplitDlls
        };
    }

    private static string? ProbeRegistry(int majorVersion)
    {
        // Pattern 1: HKLM\SOFTWARE\Siemens\Automation\InstalledApps\Totally Integrated Automation Portal V{XX}
        var installedAppsKey = $@"SOFTWARE\Siemens\Automation\InstalledApps\Totally Integrated Automation Portal V{majorVersion}";
        var path = ProbeRegistryKey(installedAppsKey, "INSTALLPATH", majorVersion);
        if (path is not null)
        {
            return path;
        }

        // Pattern 2: HKLM\SOFTWARE\Siemens\Automation\_InstalledSoftware\TIAP\{XX}.0
        var installedSoftwareKey = $@"SOFTWARE\Siemens\Automation\_InstalledSoftware\TIAP\{majorVersion}.0";
        path = ProbeRegistryKey(installedSoftwareKey, "Path", majorVersion);
        if (path is not null)
        {
            return path;
        }

        return null;
    }

    private static string? ProbeRegistryKey(string subKey, string valueName, int majorVersion)
    {
        foreach (var registryView in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
            using var key = baseKey.OpenSubKey(subKey);

            if (key is null)
            {
                continue;
            }

            var installPath = key.GetValue(valueName) as string;
            if (string.IsNullOrWhiteSpace(installPath))
            {
                // Try other common value names
                installPath = key.GetValue("INSTALLPATH") as string
                    ?? key.GetValue("Path") as string
                    ?? key.GetValue("") as string;
            }

            if (string.IsNullOrWhiteSpace(installPath))
            {
                continue;
            }

            var candidate = ResolveDllDirectory(installPath!, majorVersion);
            if (candidate is not null && Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ResolveDllDirectory(string basePath, int majorVersion)
    {
        // V21+: PublicAPI/V21/net48
        if (majorVersion >= 21)
        {
            var net48Path = Path.Combine(basePath, $"PublicAPI\\V{majorVersion}\\net48");
            if (Directory.Exists(net48Path))
            {
                return net48Path;
            }

            // Without net48 subfolder
            var apiPath = Path.Combine(basePath, $"PublicAPI\\V{majorVersion}");
            if (Directory.Exists(apiPath))
            {
                return apiPath;
            }
        }

        // V16-V19: PublicAPI/V{XX} (single Siemens.Engineering.dll)
        var standardPath = Path.Combine(basePath, $"PublicAPI\\V{majorVersion}");
        if (Directory.Exists(standardPath))
        {
            return standardPath;
        }

        // Maybe basePath itself is the DLL directory
        if (basePath.Contains("PublicAPI"))
        {
            // For V21+, check if DLLs are actually here or in net48 subfolder
            if (majorVersion >= 21)
            {
                var net48Sub = Path.Combine(basePath, "net48");
                if (Directory.Exists(net48Sub) && File.Exists(Path.Combine(net48Sub, "Siemens.Engineering.Base.dll")))
                {
                    return net48Sub;
                }
            }

            // Verify the expected DLL actually exists before returning this path
            var expectedDll = majorVersion >= 21
                ? "Siemens.Engineering.Base.dll"
                : "Siemens.Engineering.dll";
            if (File.Exists(Path.Combine(basePath, expectedDll)))
            {
                return basePath;
            }
        }

        return null;
    }
}
