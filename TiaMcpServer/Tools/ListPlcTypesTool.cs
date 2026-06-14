using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class ListPlcTypesTool
    {
        [McpServerTool(Name = "list_plc_types")]
        [Description(
            "List the PLC user types (UDTs / data types) of one PLC (or all PLCs): name and path. " +
            "Use to discover available user-defined types before referencing them in blocks. " +
            "Accepts either the device name or the PLC-software name as plcName.")]
        public static async Task<string> ListPlcTypes(
            OpennessWorkerClient workerClient,
            [Description("Optional PLC name (device name OR PLC-software name). If omitted, lists types across all PLCs.")] string? plcName = null,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.ListPlcTypesAsync(plcName, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
