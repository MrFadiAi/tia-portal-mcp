using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class ListPlcsTool
    {
        [McpServerTool(Name = "list_plcs")]
        [Description(
            "List every PLC in the project with its device name, PLC-software name, " +
            "and block/tag-table/type counts. Use this FIRST to discover which PLCs exist " +
            "and their exact names before calling list_blocks/get_block_content. " +
            "Note: the device name and PLC-software name often differ; both are accepted by plcName filters.")]
        public static async Task<string> ListPlcs(
            OpennessWorkerClient workerClient,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.ListPlcsAsync(projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
