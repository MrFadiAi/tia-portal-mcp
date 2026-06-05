using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class BrowseProjectTreeTool
    {
        [McpServerTool(Name = "browse_project_tree")]
        [Description("Recursively enumerate the TIA Portal project tree: devices, PLC software, block folders, blocks, tag tables, and types. Returns a structured JSON tree.")]
        public static async Task<string> BrowseProjectTree(
            OpennessWorkerClient workerClient,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.BrowseProjectTreeAsync(projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
