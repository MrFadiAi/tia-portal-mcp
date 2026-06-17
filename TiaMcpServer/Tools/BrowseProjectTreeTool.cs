using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class BrowseProjectTreeTool
    {
        [McpServerTool(Name = "browse_project_tree")]
        [Description("Recursively enumerate the TIA Portal project tree: devices, PLC software, block folders, blocks, tag tables, and types. Returns a structured JSON tree. For large projects, pass plcName to return only that PLC's tree (avoids truncation). For very large PLCs (a full tree can be hundreds of KB), pass maxNodes to page through the tree: the response becomes { nodes, totalCount, nextSkip } — call again with skip=nextSkip to fetch the next page until nextSkip is null. Omit maxNodes/skip to get the whole tree at once.")]
        public static async Task<string> BrowseProjectTree(
            OpennessWorkerClient workerClient,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("Optional PLC name to filter the tree (e.g. 'PLF_01A_PLC_SNIJTOOL'). When set, only that PLC's blocks/tag tables/types are returned. Use this for large projects to avoid response truncation.")] string? plcName = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null,
            [Description("Optional page size (max nodes to return). When set, the response is { nodes, totalCount, nextSkip } instead of the bare tree. Omit to return the whole tree.")] int? maxNodes = null,
            [Description("Optional continuation offset from a previous page's nextSkip. Use with maxNodes to page through a large tree.")] int? skip = null)
        {
            return await workerClient.BrowseProjectTreeAsync(projectPath, plcName, tiaVersion, maxNodes, skip).ConfigureAwait(false);
        }
    }
}
