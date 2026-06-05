using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class ListTagTablesTool
    {
        [McpServerTool(Name = "list_tag_tables")]
        [Description("Retrieve all PLC tag tables with their tags and user constants. Returns a JSON array of tag tables.")]
        public static async Task<string> ListTagTables(
            OpennessWorkerClient workerClient,
            [Description("Optional PLC device name to filter. If omitted, uses the first PLC found.")] string? plcName = null,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.ListTagTablesAsync(plcName, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
