using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class ReadHardwareConfigTool
    {
        [McpServerTool(Name = "read_hardware_config")]
        [Description(
            "Export the hardware configuration and network topology from the TIA Portal project. Returns a JSON document with all devices, their rack modules, network interfaces, IP addresses, PROFINET device names, subnets, and IO systems. " +
            "Set includeIoDetails=true to additionally report each IO module's absolute addresses and channels (channel logical address, e.g. %I4.0, is derived only from complete aligned evidence). " +
            "Set includeTagMatches=true (implies includeIoDetails) to match each channel against a PLC's tag tables by EXACT absolute address; supply plcName when the project has more than one PLC. " +
            "Note: with an explicit plcName, tag matching requires that PLC to be the channel's controller (AddressControllers association); channels without a resolvable controller get no matches. " +
            "The extended payload is capped (ioDetailsTruncated=true notes the cap). Read-only.")]
        public static async Task<string> ReadHardwareConfig(
            OpennessWorkerClient workerClient,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null,
            [Description("Include per-IO-module details: absolute addresses (I/O type, start, length, controller association) and channels (number, I/O type, type, bit address/width, formatted logical address).")] bool includeIoDetails = false,
            [Description("Match each channel against the PLC's tag tables by exact absolute address (implies includeIoDetails). With more than one PLC in the project, plcName is required to select one.")] bool includeTagMatches = false,
            [Description("PLC (software or device name) whose tag tables are matched when includeTagMatches=true. Omitted: works only when the project has exactly one PLC.")] string? plcName = null)
        {
            return await workerClient.ReadHardwareConfigAsync(
                projectPath, tiaVersion, includeIoDetails, includeTagMatches, plcName).ConfigureAwait(false);
        }
    }
}
