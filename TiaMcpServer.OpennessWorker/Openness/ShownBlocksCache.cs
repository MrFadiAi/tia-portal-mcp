using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Session-scoped record of block contents already returned by get_block_content,
/// keyed by blockPath. blockPath includes the PLC name (e.g.
/// "PLF_01A_PLC_HOOGTE_SNIJDER_2/FC_MODBUS_COMMUNICATIE") so same-named blocks on
/// different PLCs do not collide. The worker process is a singleton, so this
/// persists across requests.
/// </summary>
/// <remarks>
/// A repeat read of UNCHANGED content returns a short note instead of re-injecting
/// the full source (see <see cref="BlockRereadResponse"/>). An edited block has a
/// different content hash and is re-shown automatically — no explicit invalidation
/// needed. Siemens-free so it is unit-testable.
/// </remarks>
public static class ShownBlocksCache
{
    private static readonly Dictionary<string, string> Store = new();
    private static readonly object Lock = new();

    /// <summary>Stable SHA-256 hex hash of block content.</summary>
    public static string ContentHash(string content)
    {
        byte[] bytes;
        using (var sha = SHA256.Create())
        {
            bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        }

        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

    /// <summary>True if this exact content (same hash) was already returned for this block.</summary>
    public static bool WasAlreadyShown(string blockPath, string contentHash)
    {
        lock (Lock)
        {
            return Store.TryGetValue(blockPath, out var h) && h == contentHash;
        }
    }

    /// <summary>Record that this content was returned for the block.</summary>
    public static void Remember(string blockPath, string contentHash)
    {
        lock (Lock)
        {
            Store[blockPath] = contentHash;
        }
    }

    /// <summary>Clear all remembered blocks (used by tests).</summary>
    public static void Clear()
    {
        lock (Lock)
        {
            Store.Clear();
        }
    }
}
