using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Safety;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class UpdateBlockLogicTool
    {
        [McpServerTool(Name = "preview_update_block_logic")]
        [Description("Preview a PLC block update and return a short-lived safetyToken. Pass the token to update_block_logic after reviewing the diff.")]
        public static async Task<string> PreviewUpdateBlockLogic(
            OpennessWorkerClient workerClient,
            [Description("Block path: 'BlockName' for first PLC, or 'PLC_1/BlockName' to target a specific PLC.")] string blockPath,
            [Description("YAML content in SIMATIC SD format to import as the block source.")] string yamlContent,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            var currentState = await workerClient.GetBlockContentAsync(blockPath, projectPath, tiaVersion).ConfigureAwait(false);
            var target = new { blockPath };
            var requestedInput = new { blockPath, yamlContent };

            return WriteSafetyTooling.CreatePreview(
                "update_block_logic",
                projectPath,
                target,
                $"Update PLC block '{blockPath}'.",
                requestedInput,
                currentState,
                WriteSafetyTooling.CreateLineDiff(currentState, yamlContent));
        }

        [McpServerTool(Name = "update_block_logic")]
        [Description("Import YAML-formatted SIMATIC SD source to update or create a PLC block. Requires confirm=true and a safetyToken from preview_update_block_logic.")]
        public static async Task<string> UpdateBlockLogic(
            OpennessWorkerClient workerClient,
            [Description("Block path: 'BlockName' for first PLC, or 'PLC_1/BlockName' to target a specific PLC.")] string blockPath,
            [Description("YAML content in SIMATIC SD format to import as the block source.")] string yamlContent,
            [Description("Set to true to confirm the write operation. Required safety flag — operation is rejected when false.")] bool confirm = false,
            [Description("Safety token returned by preview_update_block_logic for this exact write request.")] string? safetyToken = null,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            if (!confirm)
                return "Operation not confirmed. Set confirm=true to proceed with the block update.";

            var target = new { blockPath };
            var requestedInput = new { blockPath, yamlContent };
            var safety = await WriteSafetyTooling.ValidateForApplyAsync(
                safetyToken,
                "preview_update_block_logic",
                "update_block_logic",
                projectPath,
                target,
                requestedInput,
                () => workerClient.GetBlockContentAsync(blockPath, projectPath, tiaVersion)).ConfigureAwait(false);
            if (!safety.IsValid)
            {
                return safety.Error!;
            }

            var result = await workerClient.UpdateBlockLogicAsync(blockPath, yamlContent, projectPath, tiaVersion).ConfigureAwait(false);
            var compileResult = result.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)
                ? null
                : await workerClient.CompileCheckAsync(blockPath, null, projectPath, tiaVersion).ConfigureAwait(false);

            WriteSafetyService.Shared.AppendAudit(
                "update_block_logic",
                projectPath,
                target,
                requestedInput,
                safety.CurrentState,
                result);

            return WriteSafetyTooling.BuildApplyResult("update_block_logic", result, "compile_check", compileResult);
        }
    }
}
