using System;
using System.Collections.Generic;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Pure (Siemens-free) pagination over an already-built <see cref="ProjectTreeNode"/> tree. A large
/// PLC's full tree can be hundreds of KB (~880 KB observed), so <c>browse_project_tree</c> offers a
/// bounded page plus a continuation offset: the caller pages through the whole tree instead of being
/// truncated. Default (no paging) returns the whole tree and is not used by the legacy code path.
/// Link-compiled into the test project.
/// </summary>
internal static class TreePagination
{
    /// <summary>
    /// Flatten the tree into DFS pre-order (a node, then its children), matching the emission order of
    /// <c>ProjectTreeWalker.Walk</c>. Null/empty <see cref="ProjectTreeNode.Children"/> are tolerated.
    /// </summary>
    public static List<ProjectTreeNode> Flatten(IReadOnlyList<ProjectTreeNode>? roots)
    {
        var flat = new List<ProjectTreeNode>();
        FlattenInto(roots, flat);
        return flat;
    }

    private static void FlattenInto(IReadOnlyList<ProjectTreeNode>? nodes, List<ProjectTreeNode> dest)
    {
        if (nodes is null)
        {
            return;
        }
        foreach (var node in nodes)
        {
            if (node is null)
            {
                continue;
            }
            dest.Add(node);
            FlattenInto(node.Children, dest);
        }
    }

    /// <summary>
    /// Return one page of the flattened tree as (<paramref name="page"/>, totalCount, nextSkip).
    /// nextSkip is null when this is the last page.
    /// <list type="bullet">
    /// <item>maxNodes null/&lt;=0 AND skip null -> return every node (nextSkip null).</item>
    /// <item>skip null/undefined -> treated as 0.</item>
    /// <item>skip &lt; 0 -> treated as 0.</item>
    /// <item>skip &gt;= totalCount -> empty page (nextSkip null).</item>
    /// </list>
    /// </summary>
    public static (List<ProjectTreeNode> Page, int TotalCount, int? NextSkip) Page(
        IReadOnlyList<ProjectTreeNode>? roots,
        int? maxNodes,
        int? skip)
    {
        var flat = Flatten(roots);
        int total = flat.Count;

        // No paging requested at all -> whole tree, no continuation.
        if ((maxNodes is null || maxNodes <= 0) && skip is null)
        {
            return (flat, total, null);
        }

        int size = (maxNodes is > 0) ? maxNodes.Value : total;
        int offset = skip is null ? 0 : Math.Max(0, skip.Value);

        if (offset >= total)
        {
            return (new List<ProjectTreeNode>(), total, null);
        }

        int take = Math.Min(size, total - offset);
        var page = flat.GetRange(offset, take);
        int? nextSkip = (offset + take < total) ? offset + take : null;
        return (page, total, nextSkip);
    }
}
