using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class CallGraphTool
    {
        [McpServerTool(Name = "call_graph")]
        [Description(
            "Callers and callees of ONE PLC block, from TIA's COMPILED cross-reference (pierces " +
            "know-how protection). Returns the blocks that call/are-called-by the target, with " +
            "reference type and location. More authoritative than search_code for call structure. " +
            "The project is compiled automatically if needed.")]
        public static async Task<string> CallGraph(
            OpennessWorkerClient workerClient,
            [Description("Block name, e.g. 'FC_MOTOR_PLUKSCHIJF' or 'FB100'.")] string block,
            [Description("Optional PLC name (device or PLC-software name). If omitted, searches all PLCs.")] string? plcName = null,
            [Description("Optional path to a TIA Portal project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.CallGraphAsync(block, plcName, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
