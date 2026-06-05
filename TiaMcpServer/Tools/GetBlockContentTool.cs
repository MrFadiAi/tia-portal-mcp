using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class GetBlockContentTool
    {
        [McpServerTool(Name = "get_block_content")]
        [Description("Export a PLC block to its SIMATIC SD (YAML) representation. Supports all block languages: SCL, LAD, FBD, GRAPH, STL, and Data Blocks.")]
        public static async Task<string> GetBlockContent(
            OpennessWorkerClient workerClient,
            [Description("Block path: 'BlockName' for first PLC, or 'PLC_1/BlockName' to target a specific PLC. Optional suffix like ' [OB1]' is stripped automatically.")] string blockPath,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.GetBlockContentAsync(blockPath, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
