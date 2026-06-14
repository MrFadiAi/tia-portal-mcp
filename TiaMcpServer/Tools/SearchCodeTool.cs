using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class SearchCodeTool
    {
        [McpServerTool(Name = "search_code")]
        [Description(
            "Full-text search across PLC block SOURCE CODE (like grep for the PLC program). " +
            "Answers 'where is X used', 'find the stop logic', 'who writes this address' in ONE call " +
            "instead of reading blocks one by one. Returns matches with block, line number, the line, " +
            "and surrounding context. Know-how-protected blocks cannot be searched (reported as skipped). " +
            "Scope to one PLC with plcName for speed on large projects.")]
        public static async Task<string> SearchCode(
            OpennessWorkerClient workerClient,
            [Description("Pattern to search for (substring), e.g. 'PLUKSCHIJF', '%Q1515.0', 'FC_MOTOR', 'STOP'.")] string query,
            [Description("Optional PLC name (device name OR PLC-software name). If omitted, searches all PLCs.")] string? plcName = null,
            [Description("Case-insensitive matching (default true).")] bool ignoreCase = true,
            [Description("Lines of context shown before/after each match (default 2, max 10).")] int contextLines = 2,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.SearchCodeAsync(query, ignoreCase, contextLines, plcName, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
