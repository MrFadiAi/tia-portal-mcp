using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class ReadBlockInterfaceTool
{
    [McpServerTool(Name = "read_block_interface")]
    [Description("Extract the interface of a PLC block: inputs, outputs, in/out, static, temp, and constant variables with data types, start values, offsets, and comments. Works for FB, FC, OB, and DB blocks (.ap16, .ap18, .ap19, .ap21).")]
    public static async Task<string> ReadBlockInterface(
        OpennessWorkerClient workerClient,
        [Description("Block path: 'BlockName', 'PLC_1/BlockName', or a deterministic path from browse_project_tree like 'PLC_1/Blocks/Folder/Block'.")] string blockPath,
        [Description("Optional PLC device name to disambiguate.")] string? plcName = null,
        [Description("Optional project path (.ap16, .ap18, .ap19, .ap21). If omitted, uses the currently open project.")] string? projectPath = null,
        [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
    {
        return await workerClient.ReadBlockInterfaceAsync(blockPath, plcName, projectPath, tiaVersion).ConfigureAwait(false);
    }
}
