using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class BrowseHmiScreensTool
{
    [McpServerTool(Name = "browse_hmi_screens")]
    [Description("Browse HMI screens from Unified HMI devices in the TIA Portal project. Use mode='list' (default) to get all screen names quickly. Use mode='detail' with a screenName to get full items, PLC tag bindings, and events for a specific screen.")]
    public static async Task<string> BrowseHmiScreens(
        OpennessWorkerClient workerClient,
        [Description("Mode: 'list' returns all screen names with item counts (fast, default). 'detail' returns full items, tag bindings, and events for a specific screen.")] string? mode = null,
        [Description("Specific screen name to get details for. Only used when mode='detail'. Ignored in list mode.")] string? screenName = null,
        [Description("Optional HMI device name to filter. If omitted, returns all HMI devices.")] string? deviceName = null,
        [Description("Optional project path.")] string? projectPath = null,
        [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
    {
        return await workerClient.BrowseHmiScreensAsync(deviceName, projectPath, mode, screenName, tiaVersion).ConfigureAwait(false);
    }
}
