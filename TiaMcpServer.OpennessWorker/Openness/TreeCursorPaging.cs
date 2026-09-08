using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Continuation state carried inside an opaque <c>browse_project_tree</c> cursor. The path is the
/// tree path (<see cref="ProjectTreeNode.Details"/>["Path"]) of the LAST node emitted on the
/// previous page; Depth/StartPath pin the query shape so a cursor from a different query cannot be
/// silently replayed against another view of the tree.
/// </summary>
internal sealed class TreeCursorState
{
    public string Path { get; set; } = string.Empty;

    public int? Depth { get; set; }

    public string? StartPath { get; set; }
}

/// <summary>Clean, user-presentable cursor failure (malformed cursor, unknown path, query mismatch).</summary>
internal sealed class TreeCursorException : Exception
{
    public TreeCursorException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Pure (Siemens-free) bounded traversal for <c>browse_project_tree</c>: deterministic ordering,
/// depth pruning, start-path selection and cursor paging over an already-built
/// <see cref="ProjectTreeNode"/> tree. The cursor is base64url JSON — deliberately NOT
/// authenticated (HMAC): this is a local/stdio server, the cursor carries no privilege, and a
/// forged cursor can only reposition a read within the same tree. Link-compiled into tests.
/// </summary>
internal static class TreeCursorPaging
{
    /// <summary>Page-size cap applied when the caller does not pass maxNodes on a bounded browse.</summary>
    public const int DefaultMaxNodes = 2000;

    private static readonly JsonSerializerOptions CursorJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string EncodeCursor(TreeCursorState state)
    {
        var json = JsonSerializer.Serialize(state, CursorJsonOptions);
        return ToBase64Url(json);
    }

    public static TreeCursorState DecodeCursor(string cursor)
    {
        try
        {
            var json = FromBase64Url(cursor);
            var state = JsonSerializer.Deserialize<TreeCursorState>(json, CursorJsonOptions);
            if (state is null || string.IsNullOrWhiteSpace(state.Path))
            {
                throw new TreeCursorException(
                    "The browse cursor is invalid — restart paging from the first page (no cursor).");
            }
            return state;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentNullException or ArgumentException)
        {
            throw new TreeCursorException(
                "The browse cursor is invalid — restart paging from the first page (no cursor).");
        }
    }

    public static bool CursorMatchesRequest(TreeCursorState state, int? depth, string? startPath)
    {
        if (state.Depth != depth)
        {
            return false;
        }

        // startPath matching mirrors TryFindStartNode: exact ordinal first, case-insensitive fallback.
        return (state.StartPath is null && string.IsNullOrWhiteSpace(startPath))
            || (state.StartPath is not null
                && startPath is not null
                && (string.Equals(state.StartPath, startPath, StringComparison.Ordinal)
                    || string.Equals(state.StartPath, startPath, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Return a copy of the tree with every children list ordered by node name
    /// (case-insensitive, ordinal tiebreak) so cursor paging is stable across calls. Input is not
    /// mutated; node instances are shared, only the children lists are rebuilt. OrderBy is stable,
    /// so equal names keep their walk order — the sequence is fully determined by the input.</summary>
    public static List<ProjectTreeNode> SortByName(IReadOnlyList<ProjectTreeNode>? roots)
    {
        if (roots is null || roots.Count == 0)
        {
            return new List<ProjectTreeNode>();
        }

        return roots
            .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(n => n.Name, StringComparer.Ordinal)
            .Select(n => n.Children is { Count: > 0 }
                ? new ProjectTreeNode { Name = n.Name, NodeType = n.NodeType, Details = n.Details, Children = SortByName(n.Children) }
                : n)
            .ToList();
    }

    /// <summary>Return a copy of the tree truncated to <paramref name="depth"/> levels (the start
    /// level counts as 1). Nodes at the boundary are shallow-cloned without children; input is not
    /// mutated.</summary>
    public static List<ProjectTreeNode> PruneToDepth(IReadOnlyList<ProjectTreeNode>? roots, int depth)
    {
        if (roots is null || roots.Count == 0)
        {
            return new List<ProjectTreeNode>();
        }

        var effectiveDepth = Math.Max(1, depth);
        return roots.Select(n => CloneAtDepth(n, effectiveDepth)).ToList();
    }

    private static ProjectTreeNode CloneAtDepth(ProjectTreeNode node, int depth)
    {
        if (depth <= 1 || node.Children is not { Count: > 0 })
        {
            return WithoutChildren(node);
        }

        return new ProjectTreeNode
        {
            Name = node.Name,
            NodeType = node.NodeType,
            Details = node.Details,
            Children = node.Children.Select(c => CloneAtDepth(c, depth - 1)).ToList()
        };
    }

    private static ProjectTreeNode WithoutChildren(ProjectTreeNode node)
        => new() { Name = node.Name, NodeType = node.NodeType, Details = node.Details };

    /// <summary>Find the node whose tree path equals <paramref name="startPath"/> (ordinal, then
    /// case-insensitive fallback). On failure <paramref name="rootNames"/> lists the valid
    /// top-level paths so the caller can correct the argument.</summary>
    public static bool TryFindStartNode(
        IReadOnlyList<ProjectTreeNode> roots,
        string startPath,
        out ProjectTreeNode? node,
        out List<string> rootNames)
    {
        node = FindByPath(roots, startPath, StringComparison.Ordinal)
            ?? FindByPath(roots, startPath, StringComparison.OrdinalIgnoreCase);
        rootNames = roots
            .Select(r => NodePath(r) ?? r.Name)
            .ToList();
        return node is not null;
    }

    private static ProjectTreeNode? FindByPath(IReadOnlyList<ProjectTreeNode> roots, string path, StringComparison comparison)
    {
        foreach (var root in roots)
        {
            if (root is null)
            {
                continue;
            }

            if (NodePath(root) is { } rootPath && string.Equals(rootPath, path, comparison))
            {
                return root;
            }

            if (root.Children is { Count: > 0 })
            {
                var found = FindByPath(root.Children, path, comparison);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    /// <summary>The node's tree path from Details["Path"], or null when the node carries none.</summary>
    public static string? NodePath(ProjectTreeNode node)
        => node.Details is not null && node.Details.TryGetValue("Path", out var path) && !string.IsNullOrEmpty(path)
            ? path
            : null;

    /// <summary>
    /// Take one page of the ordered flat node list. Resumes AFTER the node named by
    /// <paramref name="resumeAfter"/> (null = first page). Returned nodes are shallow clones with
    /// children stripped (the page is flat; hierarchy is carried by each node's path).
    /// <paramref name="depth"/>/<paramref name="startPath"/> are baked into the emitted next cursor.
    /// </summary>
    public static TreeCursorPage TakePage(
        IReadOnlyList<ProjectTreeNode> flat,
        int? maxNodes,
        TreeCursorState? resumeAfter,
        int? depth,
        string? startPath)
    {
        int resumeIndex = 0;
        if (resumeAfter is not null)
        {
            resumeIndex = IndexAfterPath(flat, resumeAfter.Path);
        }

        int cap = (maxNodes is > 0) ? maxNodes.Value : DefaultMaxNodes;
        int take = Math.Min(cap, flat.Count - resumeIndex);
        var page = new TreeCursorPage
        {
            Nodes = new List<ProjectTreeNode>(take),
            NodeCount = flat.Count,
            Truncated = resumeIndex + take < flat.Count
        };

        for (int i = resumeIndex; i < resumeIndex + take; i++)
        {
            page.Nodes.Add(WithoutChildren(flat[i]));
        }

        if (page.Truncated && page.Nodes.Count > 0)
        {
            var lastPath = NodePath(page.Nodes[page.Nodes.Count - 1]);
            if (lastPath is not null)
            {
                page.NextCursor = EncodeCursor(new TreeCursorState
                {
                    Path = lastPath,
                    Depth = depth,
                    StartPath = string.IsNullOrWhiteSpace(startPath) ? null : startPath
                });
            }
            else
            {
                // A page node without a path cannot be continued from — report the tail as
                // un-truncated rather than handing out a cursor that can never resume.
                page.Truncated = false;
            }
        }

        return page;
    }

    private static int IndexAfterPath(IReadOnlyList<ProjectTreeNode> flat, string path)
    {
        for (int i = 0; i < flat.Count; i++)
        {
            if (NodePath(flat[i]) is { } nodePath && string.Equals(nodePath, path, StringComparison.Ordinal))
            {
                return i + 1;
            }
        }

        throw new TreeCursorException(
            $"The browse cursor's position ('{path}') is not in the current tree — the project changed or the cursor is from another query. Restart paging from the first page (no cursor).");
    }

    // ------------------------------------------------------------------ base64url

    private static string ToBase64Url(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        int remainder = base64.Length % 4;
        if (remainder > 0)
        {
            base64 = base64.PadRight(base64.Length + (4 - remainder), '=');
        }
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
