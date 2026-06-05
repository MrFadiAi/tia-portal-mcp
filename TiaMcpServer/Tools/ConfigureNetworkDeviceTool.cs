using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Safety;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class ConfigureNetworkDeviceTool
    {
        [McpServerTool(Name = "preview_configure_network_device")]
        [Description("Preview network identity/interface changes for a device and return a short-lived safetyToken. Pass the token to configure_network_device after reviewing the preview.")]
        public static async Task<string> PreviewConfigureNetworkDevice(
            OpennessWorkerClient workerClient,
            [Description("Device name to configure.")] string deviceName,
            [Description("Optional IPv4 address to set on the first network node.")] string? ipAddress = null,
            [Description("Optional subnet mask to set on the first network node.")] string? subnetMask = null,
            [Description("Optional PROFINET device name to set on the first network node.")] string? pnDeviceName = null,
            [Description("Optional subnet name to connect the first network node to.")] string? subnetName = null,
            [Description("Optional IO system name to connect the device's IO connector to.")] string? ioSystemName = null,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            var currentState = await workerClient.ReadHardwareConfigAsync(projectPath, tiaVersion).ConfigureAwait(false);
            var target = new { deviceName };
            var requestedInput = new { deviceName, ipAddress, subnetMask, pnDeviceName, subnetName, ioSystemName };

            return WriteSafetyTooling.CreatePreview(
                "configure_network_device",
                projectPath,
                target,
                $"Configure network properties for device '{deviceName}'.",
                requestedInput,
                currentState);
        }

        [McpServerTool(Name = "configure_network_device")]
        [Description("Configure network identity and interface properties for a device in the TIA Portal project. Requires confirm=true and a safetyToken from preview_configure_network_device.")]
        public static async Task<string> ConfigureNetworkDevice(
            OpennessWorkerClient workerClient,
            [Description("Device name to configure.")] string deviceName,
            [Description("Optional IPv4 address to set on the first network node.")] string? ipAddress = null,
            [Description("Optional subnet mask to set on the first network node.")] string? subnetMask = null,
            [Description("Optional PROFINET device name to set on the first network node.")] string? pnDeviceName = null,
            [Description("Optional subnet name to connect the first network node to.")] string? subnetName = null,
            [Description("Optional IO system name to connect the device's IO connector to.")] string? ioSystemName = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false,
            [Description("Safety token returned by preview_configure_network_device for this exact write request.")] string? safetyToken = null,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with configuring a network device.";
            }

            var target = new { deviceName };
            var requestedInput = new { deviceName, ipAddress, subnetMask, pnDeviceName, subnetName, ioSystemName };
            var safety = await WriteSafetyTooling.ValidateForApplyAsync(
                safetyToken,
                "preview_configure_network_device",
                "configure_network_device",
                projectPath,
                target,
                requestedInput,
                () => workerClient.ReadHardwareConfigAsync(projectPath, tiaVersion)).ConfigureAwait(false);
            if (!safety.IsValid)
            {
                return safety.Error!;
            }

            var result = await workerClient.ConfigureNetworkDeviceAsync(
                deviceName,
                ipAddress,
                subnetMask,
                pnDeviceName,
                subnetName,
                ioSystemName,
                projectPath,
                tiaVersion).ConfigureAwait(false);
            var hardwareConfig = result.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)
                ? null
                : await workerClient.ReadHardwareConfigAsync(projectPath, tiaVersion).ConfigureAwait(false);

            WriteSafetyService.Shared.AppendAudit(
                "configure_network_device",
                projectPath,
                target,
                requestedInput,
                safety.CurrentState,
                result);

            return WriteSafetyTooling.BuildApplyResult("configure_network_device", result, "read_hardware_config", hardwareConfig);
        }
    }
}
