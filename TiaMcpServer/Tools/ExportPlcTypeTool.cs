using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class ExportPlcTypeTool
{
    [McpServerTool(Name = "export_plc_type")]
    [Description("Export a PLC user-defined type (UDT/struct) to XML. Returns the full type definition including members, data types, and offsets.")]
    public static async Task<string> ExportPlcType(
        OpennessWorkerClient workerClient,
        [Description("Name of the PLC type (UDT) to export.")] string typeName,
        [Description("Optional PLC device name.")] string? plcName = null,
        [Description("Optional folder path within the type group.")] string? folderPath = null,
        [Description("Optional project path.")] string? projectPath = null,
        [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
    {
        return await workerClient.ExportPlcTypeAsync(typeName, plcName, folderPath, projectPath, tiaVersion).ConfigureAwait(false);
    }
}
