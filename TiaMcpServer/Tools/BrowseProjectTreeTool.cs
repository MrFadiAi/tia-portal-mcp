using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class BrowseProjectTreeTool
    {
        [McpServerTool(Name = "browse_project_tree")]
        [Description("Recursively enumerate the TIA Portal project tree: devices, PLC software, block folders, blocks, tag tables, and types. Returns a structured JSON tree. For large projects, pass plcName to return only that PLC's tree. For very large PLCs (a full tree can be hundreds of KB), use BOUNDED PAGING: start with depth=2 to see the top structure, then page deeper with depth/startPath; the response is { nodes (a FLAT list where each node's path shows its position), nodeCount, truncated, nextCursor } — when truncated is true, call again with the same depth/startPath plus cursor=nextCursor until nextCursor is null. Use startPath (a node path from a previous page, e.g. 'MyPLC/Program blocks') to drill into one subtree. Pages are capped at 2000 nodes unless maxNodes is set lower. The legacy maxNodes/skip offset paging ({ nodes, totalCount, nextSkip }) still works; omit all paging params to get the whole tree at once.")]
        public static async Task<string> BrowseProjectTree(
            OpennessWorkerClient workerClient,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("Optional PLC name to filter the tree (e.g. 'PLF_01A_PLC_SNIJTOOL'). When set, only that PLC's blocks/tag tables/types are returned. Use this for large projects to avoid response truncation.")] string? plcName = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null,
            [Description("Optional page size (max nodes to return). Bounded mode caps pages at 2000 nodes; pass a lower maxNodes for smaller pages.")] int? maxNodes = null,
            [Description("Optional continuation offset from a previous page's nextSkip (legacy offset paging only).")] int? skip = null,
            [Description("Max levels to descend (the start level counts as 1, so depth=1 returns just the top-level nodes, depth=2 adds their direct children). Enables bounded cursor paging — recommended first call for large projects.")] int? depth = null,
            [Description("Tree path of a node to start the traversal at (e.g. 'MyPLC/Program blocks', taken from a previous page's node path). Enables bounded cursor paging; the response then covers only that subtree.")] string? startPath = null,
            [Description("Continuation cursor from a previous page's nextCursor. Pass with the SAME depth/startPath used for that page until nextCursor is null.")] string? cursor = null)
        {
            return await workerClient.BrowseProjectTreeAsync(projectPath, plcName, tiaVersion, maxNodes, skip, depth, startPath, cursor).ConfigureAwait(false);
        }
    }
}
