using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class TagUsageTool
    {
        [McpServerTool(Name = "tag_usage")]
        [Description(
            "Find every place a PLC TAG is referenced in block source (no compile needed). Resolves the " +
            "tag's logical address and ALSO searches its absolute-address forms (English + German STL, " +
            "e.g. %I301.0 / 'E 301.0'). Each block is exported via a fallback chain (ExportAsDocuments -> " +
            "Export(FileInfo)), so MOST know-how-protected blocks ARE readable; only truly-unreadable ones " +
            "are skipped (reported as skippedProtectedCount). For STL, read vs write is classified from the " +
            "instruction token. For authoritative read/write access (matching TIA's cross-reference editor), " +
            "or if references are 0/suspiciously few with skippedProtectedCount > 0, escalate to " +
            "read_cross_references (TIA's COMPILED cross-reference, pierces all protection). Do not call a " +
            "tag 'unused' while skippedProtectedCount > 0 unless read_cross_references was tried. To make " +
            "protected blocks fully readable, ask the user for the know-how password and call knowhow_unlock " +
            "(cached per-project, never asked again).")]
        public static async Task<string> TagUsage(
            OpennessWorkerClient workerClient,
            [Description("Tag name to trace, e.g. 'PLUKSCHIJF MOTOR_RUN' (quotes optional).")] string tag,
            [Description("Optional PLC name (device name OR PLC-software name). If omitted, searches all PLCs.")] string? plcName = null,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.TagUsageAsync(tag, plcName, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
