using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    /// <summary>
    /// PLC run-state control. Both tools change the RUNTIME state of the physical CPU, not the
    /// project — they are gated by a two-step confirm: without confirm the call is a dry run
    /// that reads and reports the current operating state; with confirm the transition runs.
    /// </summary>
    [McpServerToolType]
    public static class PlcOnlineTools
    {
        [McpServerTool(Name = "start_plc")]
        [Description(
            "Start the PLC: transition the target CPU to RUN (runtime-mutating — changes the physical " +
            "CPU state, not the project). First call WITHOUT confirm returns a DRY RUN: the PLC's current " +
            "operating state and what would happen. Call again with confirm=true to execute the start. " +
            "The PLC must be reachable/online (configured target) or the call fails.")]
        public static async Task<string> StartPlc(
            OpennessWorkerClient workerClient,
            [Description("Optional PLC name (device name, e.g. 'PLC_1'). Omit to use the first PLC in the project.")] string? plcName = null,
            [Description("Dry run when false (default): reports the current operating state without changing anything. Set true to execute the start.")] bool confirm = false,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.StartPlcAsync(plcName, confirm, projectPath, tiaVersion).ConfigureAwait(false);
        }

        [McpServerTool(Name = "stop_plc")]
        [Description(
            "Stop the PLC: transition the target CPU to STOP (runtime-mutating — changes the physical " +
            "CPU state, not the project). First call WITHOUT confirm returns a DRY RUN: the PLC's current " +
            "operating state and what would happen. Call again with confirm=true to execute the stop. " +
            "The PLC must be reachable/online (configured target) or the call fails.")]
        public static async Task<string> StopPlc(
            OpennessWorkerClient workerClient,
            [Description("Optional PLC name (device name, e.g. 'PLC_1'). Omit to use the first PLC in the project.")] string? plcName = null,
            [Description("Dry run when false (default): reports the current operating state without changing anything. Set true to execute the stop.")] bool confirm = false,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.StopPlcAsync(plcName, confirm, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
