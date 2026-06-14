using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class BrowseProjectTreeTool
    {
        [McpServerTool(Name = "browse_project_tree")]
        [Description("Recursively enumerate the TIA Portal project tree: devices, PLC software, block folders, blocks, tag tables, and types. Returns a structured JSON tree. For large projects, pass plcName to return only that PLC's tree (avoids truncation).")]
        public static async Task<string> BrowseProjectTree(
            OpennessWorkerClient workerClient,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("Optional PLC name to filter the tree (e.g. 'PLF_01A_PLC_SNIJTOOL'). When set, only that PLC's blocks/tag tables/types are returned. Use this for large projects to avoid response truncation.")] string? plcName = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.BrowseProjectTreeAsync(projectPath, plcName, tiaVersion).ConfigureAwait(false);
        }
    }
}
