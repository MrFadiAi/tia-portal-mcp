using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class ReadCrossReferencesTool
    {
        [McpServerTool(Name = "read_cross_references")]
        [Description("Export TIA Portal cross-reference diagnostics for PLC software. Returns JSON with source objects, referenced objects, locations, access types, reference types, and skip messages. Large projects can return large JSON; use plcName and filter to narrow the result.")]
        public static async Task<string> ReadCrossReferences(
            OpennessWorkerClient workerClient,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("Optional PLC device name. If omitted, reads cross references from all PLC software in the project.")] string? plcName = null,
            [Description("Optional cross-reference filter. Allowed values: AllObjects, ObjectsWithReferences, ObjectsWithoutReferences, UnusedObjects. Defaults to ObjectsWithReferences.")] string? filter = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.ReadCrossReferencesAsync(projectPath, plcName, filter, tiaVersion).ConfigureAwait(false);
        }
    }
}
