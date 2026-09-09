using System.Collections.Generic;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>Outcome of reading a subnet's connected nodes.</summary>
public enum ConnectedNodeReadStatus
{
    /// <summary>The Nodes collection was enumerated successfully (possibly empty).</summary>
    Readable,

    /// <summary>The Nodes property is missing, not enumerable, or threw while enumerating.
    /// Any partially collected node names are still reported, but the collection must be
    /// treated as UNVERIFIED — never as "no connected nodes".</summary>
    Unreadable,
}

/// <summary>The collected evidence: status plus every node name that could be read.</summary>
public sealed class ConnectedNodeReadResult
{
    public ConnectedNodeReadResult(ConnectedNodeReadStatus status, IReadOnlyList<string> nodes)
    {
        Status = status;
        Nodes = nodes;
    }

    public ConnectedNodeReadStatus Status { get; }

    public IReadOnlyList<string> Nodes { get; }

    public static ConnectedNodeReadResult Readable(IReadOnlyList<string> nodes)
        => new(ConnectedNodeReadStatus.Readable, nodes);

    public static ConnectedNodeReadResult Unreadable(IReadOnlyList<string> partiallyCollected)
        => new(ConnectedNodeReadStatus.Unreadable, partiallyCollected);
}

/// <summary>
/// Pure verdict mapping for delete_subnet's connected-node guard (Siemens-free, TDD'd).
/// The delete guard must never silently weaken: an unreadable node check ENGAGES the guard
/// (refuse without force, ask for manual verification) — only a successfully-read EMPTY
/// collection counts as "0 connected nodes".
/// </summary>
public static class ConnectedNodeGuard
{
    public const string UnverifiableNote =
        "connected-node check unavailable on this TIA version — verify manually before forcing";

    /// <summary>The guard decision derived from the read evidence.</summary>
    public sealed class Verdict
    {
        public Verdict(bool guardEngaged, bool requiresManualVerification)
        {
            GuardEngaged = guardEngaged;
            RequiresManualVerification = requiresManualVerification;
        }

        /// <summary>True → the delete must be refused without force=true.</summary>
        public bool GuardEngaged { get; }

        /// <summary>True → the result must carry <see cref="UnverifiableNote"/> (the node
        /// count is unverified even if the delete proceeds with force).</summary>
        public bool RequiresManualVerification { get; }
    }

    public static Verdict Evaluate(ConnectedNodeReadStatus status, int readableNodeCount)
        => status switch
        {
            ConnectedNodeReadStatus.Unreadable => new Verdict(guardEngaged: true, requiresManualVerification: true),
            ConnectedNodeReadStatus.Readable when readableNodeCount == 0 => new Verdict(false, false),
            ConnectedNodeReadStatus.Readable => new Verdict(guardEngaged: true, requiresManualVerification: false),
            _ => new Verdict(guardEngaged: true, requiresManualVerification: true),
        };
}
