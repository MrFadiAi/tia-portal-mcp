using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Contracts;
using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

public class TreePaginationTests
{
    //   A
    //   |- B
    //   |   |- C
    //   |   '- D
    //   '- E
    //   F
    //   '- G
    // DFS pre-order: A, B, C, D, E, F, G  (7 nodes)
    private static List<ProjectTreeNode> SampleTree() => new()
    {
        Node("A", Node("B", Node("C"), Node("D")), Node("E")),
        Node("F", Node("G")),
    };

    private static ProjectTreeNode Node(string name, params ProjectTreeNode[] children) => new()
    {
        Name = name,
        NodeType = name.Length == 1 ? "Block" : "Device",
        Children = children.Length == 0 ? null : new List<ProjectTreeNode>(children),
    };

    private static List<string> Names(List<ProjectTreeNode> nodes) => nodes.Select(n => n.Name).ToList();

    // ---- Flatten ----

    [Fact]
    public void Flatten_Is_Dfs_Pre_Order()
    {
        Assert.Equal(new[] { "A", "B", "C", "D", "E", "F", "G" }, Names(TreePagination.Flatten(SampleTree())));
    }

    [Fact]
    public void Flatten_Tolerates_Null_Children_And_Null_Roots()
    {
        var withNullChildren = new List<ProjectTreeNode>
        {
            new() { Name = "X", NodeType = "Block", Children = null },
        };
        Assert.Equal(new[] { "X" }, Names(TreePagination.Flatten(withNullChildren)));

        Assert.Empty(TreePagination.Flatten(null));
    }

    // ---- Page ----

    [Fact]
    public void Page_No_Paging_Returns_All_With_No_Continuation()
    {
        var (page, total, nextSkip) = TreePagination.Page(SampleTree(), null, null);

        Assert.Equal(7, page.Count);
        Assert.Equal(7, total);
        Assert.Null(nextSkip);
    }

    [Fact]
    public void Page_First_Page_Returns_PageSize_And_Continuation()
    {
        var (page, total, nextSkip) = TreePagination.Page(SampleTree(), maxNodes: 3, skip: 0);

        Assert.Equal(new[] { "A", "B", "C" }, Names(page));
        Assert.Equal(7, total);
        Assert.Equal(3, nextSkip);
    }

    [Fact]
    public void Page_Middle_Page_Advances_Continuation()
    {
        var (page, total, nextSkip) = TreePagination.Page(SampleTree(), maxNodes: 3, skip: 3);

        Assert.Equal(new[] { "D", "E", "F" }, Names(page));
        Assert.Equal(7, total);
        Assert.Equal(6, nextSkip);
    }

    [Fact]
    public void Page_Last_Page_Has_Remainder_And_Null_Continuation()
    {
        var (page, total, nextSkip) = TreePagination.Page(SampleTree(), maxNodes: 3, skip: 6);

        Assert.Equal(new[] { "G" }, Names(page));
        Assert.Equal(7, total);
        Assert.Null(nextSkip);
    }

    [Fact]
    public void Page_Skip_Past_End_Returns_Empty()
    {
        var (page, total, nextSkip) = TreePagination.Page(SampleTree(), maxNodes: 3, skip: 100);

        Assert.Empty(page);
        Assert.Equal(7, total);
        Assert.Null(nextSkip);
    }

    [Fact]
    public void Page_Negative_Skip_Treated_As_Zero()
    {
        var (page, _, nextSkip) = TreePagination.Page(SampleTree(), maxNodes: 3, skip: -5);

        Assert.Equal(new[] { "A", "B", "C" }, Names(page));
        Assert.Equal(3, nextSkip);
    }

    [Fact]
    public void Page_Skip_Only_Returns_Remainder()
    {
        // maxNodes unset, skip=2 -> from index 2 to the end, no continuation.
        var (page, total, nextSkip) = TreePagination.Page(SampleTree(), null, skip: 2);

        Assert.Equal(new[] { "C", "D", "E", "F", "G" }, Names(page));
        Assert.Equal(7, total);
        Assert.Null(nextSkip);
    }

    [Fact]
    public void Page_NonPositive_MaxNodes_And_No_Skip_Returns_All()
    {
        var (page, total, nextSkip) = TreePagination.Page(SampleTree(), maxNodes: 0, skip: null);

        Assert.Equal(7, page.Count);
        Assert.Equal(7, total);
        Assert.Null(nextSkip);
    }
}
