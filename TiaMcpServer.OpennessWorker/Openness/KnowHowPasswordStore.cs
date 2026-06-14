using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Persists the know-how protection password per project so the user only provides it ONCE.
/// The worker process is spawned fresh per call, so this is FILE-backed (not in-memory):
/// a JSON map of normalized project path -> password under %AppData%/tia-mcp/knowhow.json.
/// An env var TIA_KNOWHOW_PASSWORD overrides/supplements it for users who prefer not to type
/// in chat. The folder is user-private by Windows default ACLs; delete the file to forget.
/// </summary>
internal static class KnowHowPasswordStore
{
    private const string EnvVar = "TIA_KNOWHOW_PASSWORD";

    private static readonly object Lock = new();

    private static string StorePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "tia-mcp", "knowhow.json");

    /// <summary>Read the cached password for this project, or null.</summary>
    public static string? TryGet(string? projectPath)
    {
        var map = ReadMap();
        return map.TryGetValue(NormalizeKey(projectPath), out var pw) ? pw : null;
    }

    /// <summary>Cache the password for this project so it is never asked for again.</summary>
    public static void Set(string? projectPath, string password)
    {
        if (string.IsNullOrEmpty(projectPath))
        {
            return;
        }

        lock (Lock)
        {
            var map = ReadMap();
            map[NormalizeKey(projectPath)] = password;
            WriteMap(map);
        }
    }

    /// <summary>Env-var override, or null.</summary>
    public static string? GetFromEnv()
    {
        var v = Environment.GetEnvironmentVariable(EnvVar);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    /// <summary>Resolve the effective password: explicit value, else cache, else env var.</summary>
    public static string? Resolve(string? explicitPassword, string? projectPath)
    {
        if (!string.IsNullOrEmpty(explicitPassword))
        {
            return explicitPassword;
        }

        return TryGet(projectPath) ?? GetFromEnv();
    }

    /// <summary>Convert a plaintext password to the SecureString the TIA protection API requires.</summary>
    internal static System.Security.SecureString ToSecureString(string? s)
    {
        var ss = new System.Security.SecureString();
        if (!string.IsNullOrEmpty(s))
        {
            foreach (var c in s)
            {
                ss.AppendChar(c);
            }
        }

        ss.MakeReadOnly();
        return ss;
    }

    private static string NormalizeKey(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(projectPath.Trim()).TrimEnd('\\', '/').ToLowerInvariant();
        }
        catch
        {
            return projectPath.Trim().ToLowerInvariant();
        }
    }

    private static Dictionary<string, string> ReadMap()
    {
        try
        {
            var path = StorePath;
            if (!File.Exists(path))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return doc is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(doc, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void WriteMap(Dictionary<string, string> map)
    {
        try
        {
            var path = StorePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = false }));
        }
        catch
        {
            /* best-effort: if we can't persist, the in-call unlock still works this session. */
        }
    }
}
