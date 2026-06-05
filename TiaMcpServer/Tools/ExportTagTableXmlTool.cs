using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class ExportTagTableXmlTool
{
    [McpServerTool(Name = "export_tag_table_xml")]
    [Description("Export PLC tag tables to XML format. If tableName is omitted, exports all tag tables. Returns the full XML definition with tags, constants, and addresses.")]
    public static async Task<string> ExportTagTableXml(
        OpennessWorkerClient workerClient,
        [Description("Optional tag table name. If omitted, all tag tables are exported.")] string? tableName = null,
        [Description("Optional PLC device name.")] string? plcName = null,
        [Description("Optional folder path within the tag table group.")] string? folderPath = null,
        [Description("Optional project path.")] string? projectPath = null,
        [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
    {
        return await workerClient.ExportTagTableXmlAsync(tableName, plcName, folderPath, projectPath, tiaVersion).ConfigureAwait(false);
    }
}
