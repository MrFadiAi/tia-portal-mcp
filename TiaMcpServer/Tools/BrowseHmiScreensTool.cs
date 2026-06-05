using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class BrowseHmiScreensTool
{
    [McpServerTool(Name = "browse_hmi_screens")]
    [Description("Browse HMI screens from Unified HMI devices in the TIA Portal project. Returns screens with their items (buttons, IO fields, etc.), PLC tag bindings (dynamizations), and events.")]
    public static async Task<string> BrowseHmiScreens(
        OpennessWorkerClient workerClient,
        [Description("Optional HMI device name to filter. If omitted, returns all HMI devices.")] string? deviceName = null,
        [Description("Optional project path.")] string? projectPath = null,
        [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
    {
        return await workerClient.BrowseHmiScreensAsync(deviceName, projectPath, tiaVersion).ConfigureAwait(false);
    }
}
