using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class UpdateBlockLogicTool
    {
        [McpServerTool(Name = "update_block_logic")]
        [Description("Import YAML-formatted SIMATIC SD source to update or create a PLC block. Supports all block languages. Use get_block_content first to understand the expected YAML format.")]
        public static async Task<string> UpdateBlockLogic(
            OpennessWorkerClient workerClient,
            [Description("Block path: 'BlockName' for first PLC, or 'PLC_1/BlockName' to target a specific PLC.")] string blockPath,
            [Description("YAML content in SIMATIC SD format to import as the block source.")] string yamlContent,
            [Description("Set to true to confirm the write operation. Required safety flag — operation is rejected when false.")] bool confirm = false,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null)
        {
            if (!confirm)
                return "Operation not confirmed. Set confirm=true to proceed with the block update.";
            return await workerClient.UpdateBlockLogicAsync(blockPath, yamlContent, projectPath).ConfigureAwait(false);
        }
    }
}
