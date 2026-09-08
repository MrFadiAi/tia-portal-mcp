using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

/// <summary>
/// One page of a bounded (depth/startPath/cursor) <c>browse_project_tree</c> traversal.
/// <see cref="Nodes"/> is a FLAT list (children stripped; each node's path shows its position in
/// the tree), <see cref="NodeCount"/> is the total number of nodes in the pruned view, and
/// <see cref="NextCursor"/> continues the traversal when <see cref="Truncated"/> is true.
/// </summary>
public class TreeCursorPage
{
    public List<ProjectTreeNode> Nodes { get; set; } = new();

    public int NodeCount { get; set; }

    public bool Truncated { get; set; }

    public string? NextCursor { get; set; }
}
