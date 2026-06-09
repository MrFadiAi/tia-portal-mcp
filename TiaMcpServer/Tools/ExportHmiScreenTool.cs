using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class ExportHmiScreenTool
{
    [McpServerTool(Name = "export_hmi_screen")]
    [Description("Export an HMI screen from the TIA Portal project to XML. Returns the full XML definition of the screen including layout, elements, bindings, and events. Use this to inspect screen structure, back up screens, or prepare modifications before re-importing.")]
    public static async Task<string> ExportHmiScreen(
        OpennessWorkerClient workerClient,
        [Description("Name of the HMI device containing the screen (e.g. 'HMI_1').")] string deviceName,
        [Description("Name of the screen to export (e.g. 'START_SCHERM').")] string screenName,
        [Description("Optional project path.")] string? projectPath = null,
        [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
    {
        return await workerClient.ExportHmiScreenAsync(deviceName, screenName, projectPath, tiaVersion).ConfigureAwait(false);
    }
}
