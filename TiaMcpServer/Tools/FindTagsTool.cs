using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class FindTagsTool
    {
        [McpServerTool(Name = "find_tags")]
        [Description(
            "Search PLC tags by NAME (case-insensitive substring) across all tag tables. " +
            "Returns matches with their table, data type, and logical address. " +
            "Use this instead of list_tag_tables when you only need specific tags (e.g. find_tags('MOTOR')). " +
            "Accepts either the device name or the PLC-software name as plcName.")]
        public static async Task<string> FindTags(
            OpennessWorkerClient workerClient,
            [Description("Tag name pattern (case-insensitive substring), e.g. 'PLUKSCHIJF', 'CLOCK', 'START_OUTPUT'.")] string query,
            [Description("Optional PLC name (device name OR PLC-software name). If omitted, searches all PLCs.")] string? plcName = null,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.FindTagsAsync(query, plcName, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
