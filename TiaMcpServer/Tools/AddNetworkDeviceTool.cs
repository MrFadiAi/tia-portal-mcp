using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class AddNetworkDeviceTool
    {
        [McpServerTool(Name = "add_network_device")]
        [Description("Insert a device from the TIA Portal hardware catalog into the project. Use search_equipment_catalog first to find the exact typeIdentifier. Requires confirm=true to proceed.")]
        public static async Task<string> AddNetworkDevice(
            OpennessWorkerClient workerClient,
            [Description("Exact catalog type identifier from search_equipment_catalog.")] string typeIdentifier,
            [Description("Name for the new device.")] string deviceName,
            [Description("Name for the root device item. Defaults to deviceName when omitted.")] string? deviceItemName = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with adding a network device.";
            }

            var effectiveDeviceItemName = string.IsNullOrWhiteSpace(deviceItemName)
                ? deviceName
                : deviceItemName;

            return await workerClient.AddNetworkDeviceAsync(
                typeIdentifier,
                deviceName,
                effectiveDeviceItemName!,
                projectPath).ConfigureAwait(false);
        }
    }
}
