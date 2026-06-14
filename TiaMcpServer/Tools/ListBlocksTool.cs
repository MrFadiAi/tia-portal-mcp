using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class ListBlocksTool
    {
        [McpServerTool(Name = "list_blocks")]
        [Description(
            "List the blocks of one PLC (or all PLCs): name, type (FC/FB/OB/DB), number, " +
            "programming language, and path — WITHOUT the block code. Much smaller than " +
            "browse_project_tree when you only need to find/locate blocks. " +
            "Accepts either the device name or the PLC-software name as plcName.")]
        public static async Task<string> ListBlocks(
            OpennessWorkerClient workerClient,
            [Description("Optional PLC name (device name OR PLC-software name). If omitted, lists blocks across all PLCs.")] string? plcName = null,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.ListBlocksAsync(plcName, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
