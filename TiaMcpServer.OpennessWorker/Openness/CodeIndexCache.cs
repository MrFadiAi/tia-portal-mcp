using System;
using System.Collections.Generic;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// In-memory cache for the built block-source <see cref="IndexBuildResult"/> — the expensive
/// part of search_code / tag_usage / hmi_tag_trace, which exports every block's source. Stores
/// the LIVE index object (not serialized JSON) keyed by (projectPath, plcName) so repeated
/// calls reuse it without re-exporting. The worker process persists across requests, so the
/// first call builds and the rest hit the cache. Cleared on any project mutation (see
/// Program.cs); a short TTL guards against edits made directly inside TIA Portal.
/// </summary>
internal static class CodeIndexCache
{
    /// <summary>Cache lifetime in seconds. Mirrors WorkerCache; short enough to stay fresh.</summary>
    private const int TtlSeconds = 30;

    private static readonly object Lock = new();
    private static readonly Dictionary<string, Entry> Store = new();

    /// <summary>
    /// Return the cached index for <paramref name="key"/> if fresh, otherwise invoke
    /// <paramref name="build"/> and cache its result. The build runs OUTSIDE the lock so a slow
    /// block export never blocks other cache reads.
    /// </summary>
    public static IndexBuildResult GetOrBuild(string key, Func<IndexBuildResult> build)
    {
        var now = DateTime.UtcNow;
        lock (Lock)
        {
            if (Store.TryGetValue(key, out var entry) && entry.Expires > now)
            {
                return entry.Result;
            }

            if (Store.ContainsKey(key))
            {
                Store.Remove(key);
            }
        }

        var result = build();

        lock (Lock)
        {
            Store[key] = new Entry(result, DateTime.UtcNow.AddSeconds(TtlSeconds));
        }

        return result;
    }

    /// <summary>Drop every cached index. Called after any project mutation.</summary>
    public static void Clear()
    {
        lock (Lock)
        {
            Store.Clear();
        }
    }

    public static string BuildKey(string? projectPath, string? plcName)
        => $"{projectPath ?? ""}|{plcName ?? ""}";

    private readonly struct Entry
    {
        public Entry(IndexBuildResult result, DateTime expires)
        {
            Result = result;
            Expires = expires;
        }

        public IndexBuildResult Result { get; }

        public DateTime Expires { get; }
    }
}
