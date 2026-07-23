using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class ComparePlcBlocksTool
    {
        [McpServerTool(Name = "compare_plc_blocks")]
        [Description(
            "Compare the PLC program blocks of two TIA Portal projects (any versions, " +
            "e.g. V21 vs V18). Returns JSON: added (only in A), removed (only in B), " +
            "changed (common, different reconstructed source — carries sourceA/sourceB), " +
            "and unchanged. Matched by block name (case-insensitive).")]
        public static async Task<string> ComparePlcBlocks(
            OpennessWorkerClient workerClient,
            [Description("Side A PLC name (PLC-software name, as shown by scan_open_projects plcNames).")] string plcNameA,
            [Description("Side A project path (.ap16/.ap18/.ap21). Omit = the currently open project.")] string? projectPathA = null,
            [Description("Side A TIA version (16/18/21).")] int? tiaVersionA = null,
            [Description("Side B PLC name.")] string plcNameB = "",
            [Description("Side B project path. Omit = the currently open project.")] string? projectPathB = null,
            [Description("Side B TIA version (16/18/21).")] int? tiaVersionB = null)
        {
            return await workerClient.ComparePlcBlocksAsync(plcNameA, projectPathA, tiaVersionA, plcNameB, projectPathB, tiaVersionB).ConfigureAwait(false);
        }
    }
}
