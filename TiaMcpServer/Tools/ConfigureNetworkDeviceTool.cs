using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class ConfigureNetworkDeviceTool
    {
        [McpServerTool(Name = "configure_network_device")]
        [Description("Configure network identity and interface properties for a device in the TIA Portal project. Sets IP address, subnet mask, PROFINET device name, and connects to subnet/IO system. Requires confirm=true.")]
        public static async Task<string> ConfigureNetworkDevice(
            OpennessWorkerClient workerClient,
            [Description("Device name to configure.")] string deviceName,
            [Description("Optional IPv4 address to set on the first network node.")] string? ipAddress = null,
            [Description("Optional subnet mask to set on the first network node.")] string? subnetMask = null,
            [Description("Optional PROFINET device name to set on the first network node.")] string? pnDeviceName = null,
            [Description("Optional subnet name to connect the first network node to.")] string? subnetName = null,
            [Description("Optional IO system name to connect the device's IO connector to.")] string? ioSystemName = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with configuring a network device.";
            }

            return await workerClient.ConfigureNetworkDeviceAsync(
                deviceName,
                ipAddress,
                subnetMask,
                pnDeviceName,
                subnetName,
                ioSystemName,
                projectPath).ConfigureAwait(false);
        }
    }
}
