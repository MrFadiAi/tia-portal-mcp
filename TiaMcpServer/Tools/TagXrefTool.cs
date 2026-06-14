using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class TagXrefTool
    {
        [McpServerTool(Name = "tag_xref")]
        [Description(
            "Authoritative read/write locations for ONE PLC tag, from TIA's COMPILED cross-reference " +
            "(pierces know-how protection). Returns each referencing block + network + access " +
            "(Read/Write/ReadWrite). Use this instead of tag_usage when you need EXACT read/write access, " +
            "when blocks are know-how-protected, or when tag_usage returned suspiciously few references. " +
            "The project is compiled automatically if needed.")]
        public static async Task<string> TagXref(
            OpennessWorkerClient workerClient,
            [Description("Tag name, e.g. 'AFPAKKER_INSTALLATIE_DRAAIT'.")] string tag,
            [Description("Optional PLC name (device or PLC-software name). If omitted, searches all PLCs.")] string? plcName = null,
            [Description("Optional path to a TIA Portal project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.TagXrefAsync(tag, plcName, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
