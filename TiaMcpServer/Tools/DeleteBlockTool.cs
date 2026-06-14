using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Safety;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class DeleteBlockTool
    {
        [McpServerTool(Name = "preview_delete_block")]
        [Description(
            "Preview deleting a PLC block and return a short-lived safetyToken. " +
            "Shows the block's current content (what will be removed). " +
            "Pass the token to delete_block after reviewing.")]
        public static async Task<string> PreviewDeleteBlock(
            OpennessWorkerClient workerClient,
            [Description("Block path: 'BlockName' for first PLC, or 'PLC_1/BlockName' to target a specific PLC.")] string blockPath,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            var currentState = await workerClient.GetBlockContentAsync(blockPath, projectPath, tiaVersion).ConfigureAwait(false);
            var target = new { blockPath };
            var requestedInput = new { blockPath, action = "delete" };

            return WriteSafetyTooling.CreatePreview(
                "delete_block",
                projectPath,
                target,
                $"Delete PLC block '{blockPath}'. This removes the block from the project.",
                requestedInput,
                currentState,
                diff: "All lines below will be REMOVED (the block is deleted entirely).");
        }

        [McpServerTool(Name = "delete_block")]
        [Description(
            "Delete a PLC block from the project. Destructive — requires confirm=true and a " +
            "safetyToken from preview_delete_block. Use list_blocks first to find the exact block path.")]
        public static async Task<string> DeleteBlock(
            OpennessWorkerClient workerClient,
            [Description("Block path: 'BlockName' for first PLC, or 'PLC_1/BlockName' to target a specific PLC.")] string blockPath,
            [Description("Set to true to confirm the deletion. Required safety flag — operation is rejected when false.")] bool confirm = false,
            [Description("Safety token returned by preview_delete_block for this exact delete request.")] string? safetyToken = null,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with the block deletion.";
            }

            var target = new { blockPath };
            var requestedInput = new { blockPath, action = "delete" };
            var safety = await WriteSafetyTooling.ValidateForApplyAsync(
                safetyToken,
                "preview_delete_block",
                "delete_block",
                projectPath,
                target,
                requestedInput,
                () => workerClient.GetBlockContentAsync(blockPath, projectPath, tiaVersion)).ConfigureAwait(false);
            if (!safety.IsValid)
            {
                return safety.Error!;
            }

            var result = await workerClient.DeleteBlockAsync(blockPath, projectPath, tiaVersion).ConfigureAwait(false);

            WriteSafetyService.Shared.AppendAudit(
                "delete_block",
                projectPath,
                target,
                requestedInput,
                safety.CurrentState,
                result);

            return WriteSafetyTooling.BuildApplyResult("delete_block", result);
        }
    }
}
