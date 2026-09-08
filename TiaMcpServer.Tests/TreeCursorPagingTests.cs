using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Contracts;
using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// Unit tests for the pure bounded-traversal helpers behind page-able browse_project_tree:
/// cursor codec, deterministic ordering, depth pruning, start-path selection and cursor paging.
/// The cursor-stability guarantee (pages cover the whole list exactly once, in order) is asserted
/// end-to-end over a multi-page walk.
/// </summary>
public sealed class TreeCursorPagingTests
{
    private static ProjectTreeNode Node(string name, string? path = null, params ProjectTreeNode[] children)
    {
        var node = new ProjectTreeNode { Name = name, NodeType = "Test", Children = children.ToList() };
        if (path is not null)
        {
            node.Details = new Dictionary<string, string> { ["Path"] = path };
        }
        return node;
    }

    private static ProjectTreeNode SampleTree() => Node(
        "DeviceB",
        "DeviceB",
        Node("PLC Software", "DeviceB/PLC Software"),
        Node("zBlocks", "DeviceB/zBlocks"));

    // ---------------------------------------------------------------- cursor codec

    [Fact]
    public void EncodeDecodeCursorRoundTripsState()
    {
        var state = new TreeCursorState { Path = "PLC_1/Blocks/FC1", Depth = 3, StartPath = "PLC_1/Blocks" };
        var decoded = TreeCursorPaging.DecodeCursor(TreeCursorPaging.EncodeCursor(state));
        Assert.Equal("PLC_1/Blocks/FC1", decoded.Path);
        Assert.Equal(3, decoded.Depth);
        Assert.Equal("PLC_1/Blocks", decoded.StartPath);
    }

    [Theory]
    [InlineData("not-a-cursor!!!")]
    [InlineData("")]
    [InlineData("   ")]
    public void DecodeMalformedCursorThrowsCleanException(string cursor)
    {
        var ex = Assert.Throws<TreeCursorException>(() => TreeCursorPaging.DecodeCursor(cursor));
        Assert.Contains("restart", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecodeBase64OfNonJsonThrowsCleanException()
    {
        var notJson = ToBase64Url("this is not json");
        Assert.Throws<TreeCursorException>(() => TreeCursorPaging.DecodeCursor(notJson));
    }

    [Fact]
    public void DecodeCursorMissingPathThrowsCleanException()
    {
        var noPath = ToBase64Url("{\"depth\":2}");
        Assert.Throws<TreeCursorException>(() => TreeCursorPaging.DecodeCursor(noPath));
    }

    private static string ToBase64Url(string value)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    // ---------------------------------------------------------------- query-shape pinning

    [Fact]
    public void CursorMatchesRequestValidatesDepthAndStartPath()
    {
        var state = new TreeCursorState { Path = "P", Depth = 2, StartPath = "PLC_1" };
        Assert.True(TreeCursorPaging.CursorMatchesRequest(state, 2, "PLC_1"));
        // startPath comparison is case-insensitive; depth must be identical.
        Assert.True(TreeCursorPaging.CursorMatchesRequest(state, 2, "plc_1"));
        Assert.False(TreeCursorPaging.CursorMatchesRequest(state, 3, "PLC_1"));
        Assert.False(TreeCursorPaging.CursorMatchesRequest(state, 2, "PLC_2"));
        Assert.False(TreeCursorPaging.CursorMatchesRequest(state, null, "PLC_1"));
        // A cursor issued without a startPath must not resume a startPath query and vice versa.
        Assert.False(TreeCursorPaging.CursorMatchesRequest(new TreeCursorState { Path = "P", Depth = 2 }, 2, "PLC_1"));
    }

    // ---------------------------------------------------------------- deterministic ordering

    [Fact]
    public void SortByNameOrdersChildrenRecursivelyCaseInsensitive()
    {
        var tree = new List<ProjectTreeNode>
        {
            Node("b2", "b2", Node("zChild", "b2/zChild"), Node("aChild", "b2/aChild")),
            Node("A1", "A1"),
            Node("a3", "a3", Node("b", "a3/b"), Node("A", "a3/A")),
        };

        var sorted = TreeCursorPaging.SortByName(tree);

        // Case-insensitive name order at the root (a1 < a3 < b2)...
        Assert.Equal(new[] { "A1", "a3", "b2" }, sorted.Select(n => n.Name).ToArray());
        // ...and recursively within each node's children.
        Assert.Equal(new[] { "A", "b" }, sorted[1].Children!.Select(n => n.Name).ToArray());
        Assert.Equal(new[] { "aChild", "zChild" }, sorted[2].Children!.Select(n => n.Name).ToArray());
    }

    [Fact]
    public void SortByNameDoesNotMutateInputTree()
    {
        var tree = new List<ProjectTreeNode> { Node("z", "z"), Node("a", "a") };
        var originalOrder = tree.Select(n => n.Name).ToList();

        TreeCursorPaging.SortByName(tree);

        Assert.Equal(originalOrder, tree.Select(n => n.Name).ToList());
    }

    // ---------------------------------------------------------------- depth pruning

    [Fact]
    public void PruneToDepthOneKeepsOnlyRootNodes()
    {
        var tree = new List<ProjectTreeNode> { SampleTree() };
        var pruned = TreeCursorPaging.PruneToDepth(tree, 1);
        var node = Assert.Single(pruned);
        Assert.Equal("DeviceB", node.Name);
        Assert.Null(node.Children);
    }

    [Fact]
    public void PruneToDepthTwoDropsGrandchildren()
    {
        var tree = new List<ProjectTreeNode>
        {
            Node("root", "root",
                Node("child", "root/child", Node("grandchild", "root/child/grandchild"))),
        };

        var pruned = TreeCursorPaging.PruneToDepth(tree, 2);
        Assert.Equal("root", pruned[0].Name);
        var child = Assert.Single(pruned[0].Children!);
        Assert.Equal("child", child.Name);
        Assert.Null(child.Children);
    }

    [Fact]
    public void PruneToDepthLargeKeepsWholeTree()
    {
        var tree = new List<ProjectTreeNode> { SampleTree() };
        var pruned = TreeCursorPaging.PruneToDepth(tree, 99);
        Assert.Equal(2, pruned[0].Children!.Count);
    }

    [Fact]
    public void PruneToDepthDoesNotMutateInputTree()
    {
        var tree = new List<ProjectTreeNode> { SampleTree() };
        TreeCursorPaging.PruneToDepth(tree, 1);
        Assert.Equal(2, tree[0].Children!.Count);
    }

    // ---------------------------------------------------------------- start-path selection

    [Fact]
    public void TryFindStartNodeMatchesByPath()
    {
        var blocks = Node("Blocks", "PLC_1/Blocks");
        var tree = new List<ProjectTreeNode> { Node("PLC_1", "PLC_1", blocks) };

        var found = TreeCursorPaging.TryFindStartNode(tree, "PLC_1/Blocks", out var node, out _);

        Assert.True(found);
        Assert.Same(blocks, node);
    }

    [Fact]
    public void TryFindStartNodeFallsBackToCaseInsensitive()
    {
        var tree = new List<ProjectTreeNode> { Node("PLC_1", "PLC_1") };
        Assert.True(TreeCursorPaging.TryFindStartNode(tree, "plc_1", out _, out _));
    }

    [Fact]
    public void TryFindStartNodeNotFoundListsRootPaths()
    {
        var tree = new List<ProjectTreeNode>
        {
            Node("PLC_1", "PLC_1"),
            Node("HMI_1", "HMI_1"),
        };

        var found = TreeCursorPaging.TryFindStartNode(tree, "nope", out var node, out var rootNames);

        Assert.False(found);
        Assert.Null(node);
        Assert.Equal(new[] { "PLC_1", "HMI_1" }, rootNames);
    }

    // ---------------------------------------------------------------- cursor paging

    private static List<ProjectTreeNode> FlatTree(int count)
        => Enumerable.Range(1, count)
            .Select(i => Node("N" + i, "R/N" + i))
            .ToList();

    [Fact]
    public void TakePageFirstPageRespectsMaxNodesAndSetsCursor()
    {
        var flat = FlatTree(10);

        var page = TreeCursorPaging.TakePage(flat, 4, null, depth: 2, startPath: null);

        Assert.Equal(4, page.Nodes.Count);
        Assert.Equal(10, page.NodeCount);
        Assert.True(page.Truncated);
        Assert.NotNull(page.NextCursor);
        var state = TreeCursorPaging.DecodeCursor(page.NextCursor!);
        Assert.Equal("R/N4", state.Path);
        Assert.Equal(2, state.Depth);
    }

    [Fact]
    public void TakePageResumeContinuesAfterCursorNode()
    {
        var flat = FlatTree(10);
        var first = TreeCursorPaging.TakePage(flat, 4, null, null, null);

        var second = TreeCursorPaging.TakePage(flat, 4, TreeCursorPaging.DecodeCursor(first.NextCursor!), null, null);

        Assert.Equal(new[] { "N5", "N6", "N7", "N8" }, second.Nodes.Select(n => n.Name).ToArray());
        Assert.True(second.Truncated);
    }

    [Fact]
    public void TakePagePagesCoverWholeListWithoutGapsOrDuplicates()
    {
        var flat = FlatTree(17);

        var walked = new List<string>();
        TreeCursorState? resume = null;
        int pages = 0;
        while (true)
        {
            var page = TreeCursorPaging.TakePage(flat, 5, resume, null, null);
            walked.AddRange(page.Nodes.Select(n => n.Name));
            pages++;
            if (!page.Truncated)
            {
                Assert.Null(page.NextCursor);
                break;
            }
            Assert.NotNull(page.NextCursor);
            resume = TreeCursorPaging.DecodeCursor(page.NextCursor!);
        }

        Assert.Equal(4, pages);
        // Exactly the original list, in order: no node skipped, none duplicated.
        Assert.Equal(Enumerable.Range(1, 17).Select(i => "N" + i).ToList(), walked);
    }

    [Fact]
    public void TakePageLastPageNotTruncated()
    {
        var flat = FlatTree(6);

        var page = TreeCursorPaging.TakePage(flat, 4, null, null, null);
        Assert.True(page.Truncated);

        var last = TreeCursorPaging.TakePage(flat, 4, TreeCursorPaging.DecodeCursor(page.NextCursor!), null, null);
        Assert.Equal(new[] { "N5", "N6" }, last.Nodes.Select(n => n.Name).ToArray());
        Assert.False(last.Truncated);
        Assert.Null(last.NextCursor);
    }

    [Fact]
    public void TakePageCursorAtEndReturnsEmptyFinalPage()
    {
        var flat = FlatTree(3);
        var resume = new TreeCursorState { Path = "R/N3" };

        var page = TreeCursorPaging.TakePage(flat, 4, resume, null, null);

        Assert.Empty(page.Nodes);
        Assert.Equal(3, page.NodeCount);
        Assert.False(page.Truncated);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public void TakePageUnknownCursorPathThrowsCleanException()
    {
        var flat = FlatTree(3);
        var resume = new TreeCursorState { Path = "R/GONE" };

        var ex = Assert.Throws<TreeCursorException>(
            () => TreeCursorPaging.TakePage(flat, 4, resume, null, null));
        Assert.Contains("restart", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TakePageDefaultsToTwoThousandNodesWhenMaxNodesOmitted()
    {
        var flat = FlatTree(2001);

        var page = TreeCursorPaging.TakePage(flat, null, null, null, null);

        Assert.Equal(TreeCursorPaging.DefaultMaxNodes, page.Nodes.Count);
        Assert.Equal(2001, page.NodeCount);
        Assert.True(page.Truncated);
    }

    [Fact]
    public void TakePageStripsChildrenFromReturnedNodes()
    {
        var flat = new List<ProjectTreeNode>
        {
            Node("root", "root", Node("child", "root/child")),
        };

        var page = TreeCursorPaging.TakePage(flat, 10, null, null, null);

        var root = Assert.Single(page.Nodes);
        Assert.Equal("root", root.Name);
        Assert.Null(root.Children);
        // The source tree is untouched.
        Assert.Single(flat[0].Children!);
    }

    [Fact]
    public void TakePageEmptyListReturnsEmptyPage()
    {
        var page = TreeCursorPaging.TakePage(new List<ProjectTreeNode>(), 10, null, null, null);
        Assert.Empty(page.Nodes);
        Assert.Equal(0, page.NodeCount);
        Assert.False(page.Truncated);
        Assert.Null(page.NextCursor);
    }
}
