using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    /// <summary>
    /// PLC block-folder (group) lifecycle. Both tools are write operations gated by a two-step
    /// confirm: without confirm the worker resolves the target and returns a preview (creates:
    /// existence check; deletes: block/subgroup blast-radius counts); with confirm they execute.
    /// </summary>
    [McpServerToolType]
    public static class BlockGroupTools
    {
        [McpServerTool(Name = "create_block_group")]
        [Description(
            "Create a PLC block folder (group). The groupPath must be the FULL deterministic path ending in " +
            "the NEW group name, e.g. 'PLC_1/Blocks/MyGroup' (root child) or 'PLC_1/Blocks/Parent/MyGroup' " +
            "(nested); software units work too: 'PLC_1/Units/UnitName/Blocks/MyGroup'. First call WITHOUT " +
            "confirm is a DRY RUN that verifies the parent exists and the name is free; call again with " +
            "confirm=true to create.")]
        public static async Task<string> CreateBlockGroup(
            OpennessWorkerClient workerClient,
            [Description("Full group path ending in the new group name, e.g. 'PLC_1/Blocks/Parent/MyGroup'.")] string groupPath,
            [Description("Dry run when false (default): checks the parent exists and the name is free. Set true to create the group.")] bool confirm = false,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.CreateBlockGroupAsync(groupPath, confirm, projectPath, tiaVersion).ConfigureAwait(false);
        }

        [McpServerTool(Name = "delete_block_group")]
        [Description(
            "Delete a PLC block folder (group) AND EVERYTHING INSIDE IT (blocks and nested groups — " +
            "destructive). The groupPath is the full deterministic path of the group, e.g. " +
            "'PLC_1/Blocks/MyGroup'. First call WITHOUT confirm is a DRY RUN that reports the blast radius " +
            "(direct blocks, nested groups, total blocks); call again with confirm=true to delete. Only " +
            "user-created groups can be deleted.")]
        public static async Task<string> DeleteBlockGroup(
            OpennessWorkerClient workerClient,
            [Description("Full group path of the group to delete, e.g. 'PLC_1/Blocks/MyGroup'.")] string groupPath,
            [Description("Dry run when false (default): reports the child counts (blast radius). Set true to delete the group and its contents.")] bool confirm = false,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.DeleteBlockGroupAsync(groupPath, confirm, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
