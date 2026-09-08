using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using Microsoft.Win32;

namespace TiaMcpServer.Diagnostics;

/// <summary>Thin, constructor-injected system surface. Checks depend on these interfaces, tests
/// depend on fakes — no real registry, process or identity access ever runs under test.</summary>

// --- Registry (HKLM, 64-bit view first, 32-bit fallback — mirrors how the host probes Siemens keys) ---
public interface IRegistryReader
{
    /// <summary>Read a REG_SZ value; null when the key or value is missing.</summary>
    string? GetLocalMachineString(string subKey, string valueName);

    /// <summary>Read a REG_DWORD/QWORD value; null when missing or not numeric.</summary>
    long? GetLocalMachineInt64(string subKey, string valueName);

    /// <summary>Sub-key names of a key; empty when the key is missing.</summary>
    IReadOnlyList<string> GetLocalMachineSubKeyNames(string subKey);

    /// <summary>All string values of a key as name/data pairs; empty when the key is missing.</summary>
    IReadOnlyList<KeyValuePair<string, string>> GetLocalMachineStringValues(string subKey);
}

// --- Processes ---
public interface IProcessLister
{
    IReadOnlyList<string> GetProcessNames();
}

// --- Current user / group membership ---
public interface IUserIdentity
{
    string? UserName { get; }

    /// <summary>Group account names (e.g. "MACHINE\Siemens TIA Openness"); null when enumeration
    /// failed (the check turns that into a Warn, never a crash).</summary>
    IReadOnlyList<string>? TryGetGroupNames();
}

// --- Filesystem facts the checks need (existence, mtimes, versions, hashes, discovery) ---
public interface IFileSystemProbe
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    /// <summary>Last-write time in UTC; null when the file is missing or unreadable.</summary>
    DateTimeOffset? GetLastWriteTimeUtc(string path);

    /// <summary>File version string (e.g. "1.2.3.0"); null when missing or unreadable.</summary>
    string? GetFileVersion(string path);

    /// <summary>SHA-256 of the file bytes as base64 — the exact format TIA's Openness whitelist
    /// stores (see scripts/tia-openness-whitelist.ps1). Null when unreadable.</summary>
    string? ComputeSha256Base64(string path);

    /// <summary>Recursive file search under <paramref name="root"/> (missing root → empty).
    /// Inaccessible directories are skipped rather than thrown.</summary>
    IReadOnlyList<string> FindFiles(string root, string searchPattern);
}

// --- Host process / OS facts ---
public interface IEnvironmentInfo
{
    bool IsWindows { get; }

    string OsVersionDescription { get; }

    string RuntimeVersion { get; }

    string HostVersion { get; }

    string? HostExePath { get; }

    string BaseDirectory { get; }
}

// --- Real implementations (used by DoctorFactory; never touched by unit tests) ---

public sealed class WindowsRegistryReader : IRegistryReader
{
    public string? GetLocalMachineString(string subKey, string valueName)
        => Probe(subKey, key => key.GetValue(valueName) as string);

    public long? GetLocalMachineInt64(string subKey, string valueName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(subKey);
                if (key is null)
                {
                    continue;
                }

                switch (key.GetValue(valueName))
                {
                    case int i:
                        return i;
                    case long l:
                        return l;
                }
            }
            catch
            {
                // Try the other view; a missing/unreadable key is a "not found", never a crash.
            }
        }

        return null;
    }

    public IReadOnlyList<string> GetLocalMachineSubKeyNames(string subKey)
        => Probe(subKey, key => (IReadOnlyList<string>?)key.GetSubKeyNames()) ?? Array.Empty<string>();

    public IReadOnlyList<KeyValuePair<string, string>> GetLocalMachineStringValues(string subKey)
        => Probe(subKey, key =>
        {
            var values = new List<KeyValuePair<string, string>>();
            foreach (var name in key.GetValueNames())
            {
                if (key.GetValue(name) is string s)
                {
                    values.Add(new KeyValuePair<string, string>(name, s));
                }
            }

            return (IReadOnlyList<KeyValuePair<string, string>>)values;
        }) ?? Array.Empty<KeyValuePair<string, string>>();

    private static T? Probe<T>(string subKey, Func<RegistryKey, T?> read) where T : class
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(subKey);
                if (key is null)
                {
                    continue;
                }

                var value = read(key);
                if (value is not null)
                {
                    return value;
                }
            }
            catch
            {
                // Try the other view; a missing/unreadable key is a "not found", never a crash.
            }
        }

        return null;
    }
}

public sealed class SystemProcessLister : IProcessLister
{
    public IReadOnlyList<string> GetProcessNames()
    {
        try
        {
            return Process.GetProcesses().Select(p => p.ProcessName).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

public sealed class CurrentUserIdentity : IUserIdentity
{
    public string? UserName
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return identity.Name;
            }
            catch
            {
                return null;
            }
        }
    }

    public IReadOnlyList<string>? TryGetGroupNames()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            if (identity.Groups is null)
            {
                return Array.Empty<string>();
            }

            var names = new List<string>();
            foreach (var sid in identity.Groups)
            {
                try
                {
                    names.Add(sid.Translate(typeof(NTAccount)).Value);
                }
                catch (IdentityNotMappedException)
                {
                    // Well-known SIDs that cannot map to a name are irrelevant here.
                }
            }

            return names;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class RealFileSystemProbe : IFileSystemProbe
{
    public bool FileExists(string path)
    {
        try { return File.Exists(path); }
        catch { return false; }
    }

    public bool DirectoryExists(string path)
    {
        try { return Directory.Exists(path); }
        catch { return false; }
    }

    public DateTimeOffset? GetLastWriteTimeUtc(string path)
    {
        try { return File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path)) : null; }
        catch { return null; }
    }

    public string? GetFileVersion(string path)
    {
        try { return FileVersionInfo.GetVersionInfo(path).FileVersion; }
        catch { return null; }
    }

    public string? ComputeSha256Base64(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            return Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(bytes));
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<string> FindFiles(string root, string searchPattern)
    {
        try
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                MatchType = MatchType.Win32
            };
            return new DirectoryInfo(root)
                .EnumerateFiles(searchPattern, options)
                .Select(f => f.FullName)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

public sealed class EnvironmentInfo : IEnvironmentInfo
{
    public bool IsWindows => OperatingSystem.IsWindows();

    public string OsVersionDescription => Environment.OSVersion.VersionString;

    public string RuntimeVersion => Environment.Version.ToString();

    public string HostVersion
        => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

    public string? HostExePath => Environment.ProcessPath;

    public string BaseDirectory => AppContext.BaseDirectory;
}
