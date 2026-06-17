using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

/// <summary>
/// One page of a paginated <c>browse_project_tree</c> response. <see cref="NextSkip"/> is the offset
/// to pass as <c>skip</c> on the next call to fetch the following page; it is null when this is the
/// last page. Only returned when the caller passes <c>maxNodes</c>; otherwise browse returns the bare
/// tree array unchanged.
/// </summary>
public class TreePage
{
    public List<ProjectTreeNode> Nodes { get; set; } = new();

    public int TotalCount { get; set; }

    public int? NextSkip { get; set; }
}
