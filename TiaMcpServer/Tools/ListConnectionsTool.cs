using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class ListConnectionsTool
{
    [McpServerTool(Name = "list_connections")]
    [Description("List network connections in the TIA Portal project: subnets, PROFINET IO systems, device network interfaces, and IP addresses.")]
    public static async Task<string> ListConnections(
        OpennessWorkerClient workerClient,
        [Description("Optional project path.")] string? projectPath = null,
        [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
    {
        return await workerClient.ListConnectionsAsync(projectPath, tiaVersion).ConfigureAwait(false);
    }
}
