using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class HmiTagTraceTool
    {
        [McpServerTool(Name = "hmi_tag_trace")]
        [Description(
            "Trace HMI screen elements → PLC tags → block code in one call. For each screen element " +
            "bound to a PLC tag (direct tag binding or animation), resolves the block/line references to " +
            "that tag. Answers 'which button writes which tag, and where is that tag used in the PLC code'. " +
            "Requires the HMI elements to have tag bindings (buttons driven by scripts return none). " +
            "Pass plcName to scope the block search to one PLC for speed.")]
        public static async Task<string> HmiTagTrace(
            OpennessWorkerClient workerClient,
            [Description("Optional HMI device name (e.g. 'PLF-01A-HMI_MTP700'). If omitted, traces all HMI devices.")] string? deviceName = null,
            [Description("Optional screen name to limit the trace to one screen.")] string? screenName = null,
            [Description("Optional PLC name (device name OR PLC-software name) to scope block-code search. If omitted, searches all PLCs.")] string? plcName = null,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.HmiTagTraceAsync(deviceName, screenName, plcName, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
