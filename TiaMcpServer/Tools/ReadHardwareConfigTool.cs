using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class ReadHardwareConfigTool
    {
        [McpServerTool(Name = "read_hardware_config")]
        [Description("Export the hardware configuration and network topology from the TIA Portal project. Returns a JSON document with all devices, their rack modules, network interfaces, IP addresses, PROFINET device names, subnets, and IO systems.")]
        public static async Task<string> ReadHardwareConfig(
            OpennessWorkerClient workerClient,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.ReadHardwareConfigAsync(projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
