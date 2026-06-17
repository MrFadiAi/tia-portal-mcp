using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class ExportTagTableJsonTool
{
    [McpServerTool(Name = "export_tag_table_json")]
    [Description("Export PLC tag tables as compact structured JSON (name, address, dataType, comment) instead of raw XML. If tableName is omitted, returns all tag tables. Use this (not export_tag_table_xml) when you need the tag data for analysis — it is much smaller and includes comments.")]
    public static async Task<string> ExportTagTableJson(
        OpennessWorkerClient workerClient,
        [Description("Optional tag table name. If omitted, all tag tables are returned.")] string? tableName = null,
        [Description("Optional PLC device name.")] string? plcName = null,
        [Description("Optional folder path within the tag table group.")] string? folderPath = null,
        [Description("Optional project path.")] string? projectPath = null,
        [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
    {
        return await workerClient.ExportTagTableJsonAsync(tableName, plcName, folderPath, projectPath, tiaVersion).ConfigureAwait(false);
    }
}
