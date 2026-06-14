using System;
using System.Collections.Generic;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// In-memory result cache for expensive structural reads (project tree, PLC/block/type
/// listings). The worker process persists across requests, so caching these avoids
/// re-walking the project every turn. Mutations invalidate the whole cache; a TTL guards
/// against external edits made directly in TIA Portal.
/// </summary>
internal static class WorkerCache
{
    private static readonly object Lock = new();
    private static readonly Dictionary<string, Entry> Store = new();

    /// <summary>Cache lifetime in seconds. Short enough to stay fresh against direct TIA edits.</summary>
    private const int TtlSeconds = 30;

    /// <summary>Methods whose results are safe to cache (pure structural reads).</summary>
    private static readonly HashSet<string> CacheableMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "browse_project_tree",
        "list_plcs",
        "list_blocks",
        "list_plc_types",
    };

    /// <summary>Methods that mutate project state and must invalidate the cache.</summary>
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "open_project", "create_project", "save_project", "save_project_as",
        "archive_project", "close_project",
        "update_block_logic", "delete_block",
        "create_tag_table", "delete_tag_table",
        "create_tag", "update_tag", "delete_tag",
        "create_user_constant", "update_user_constant", "delete_user_constant",
        "add_network_device", "configure_network_device",
        "import_hmi_screen",
        "knowhow_unlock",
    };

    public static bool IsCacheable(string method) => CacheableMethods.Contains(method);
    public static bool IsMutating(string method) => MutatingMethods.Contains(method);

    public static string? TryGet(string key)
    {
        var now = DateTime.UtcNow;
        lock (Lock)
        {
            if (Store.TryGetValue(key, out var entry) && entry.Expires > now)
            {
                return entry.Payload;
            }

            if (Store.ContainsKey(key))
            {
                Store.Remove(key);
            }

            return null;
        }
    }

    public static void Set(string key, string payload)
    {
        lock (Lock)
        {
            Store[key] = new Entry(payload, DateTime.UtcNow.AddSeconds(TtlSeconds));
        }
    }

    public static void Invalidate()
    {
        lock (Lock)
        {
            Store.Clear();
        }
    }

    public static string BuildKey(string method, string? projectPath, int? tiaVersion, string? plcName)
        => $"{method}|{projectPath ?? ""}|{tiaVersion?.ToString() ?? ""}|{plcName ?? ""}";

    private readonly struct Entry
    {
        public Entry(string payload, DateTime expires)
        {
            Payload = payload;
            Expires = expires;
        }

        public string Payload { get; }
        public DateTime Expires { get; }
    }
}
