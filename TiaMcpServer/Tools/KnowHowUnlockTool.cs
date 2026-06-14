using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class KnowHowUnlockTool
    {
        [McpServerTool(Name = "knowhow_unlock")]
        [Description(
            "Remove know-how protection from PLC blocks so their source becomes readable, via the official " +
            "TIA PlcBlockProtectionProvider.Unprotect API. Call this when a read tool (tag_usage / search_code / " +
            "get_block_content) reports know-how-protected blocks you need to see, OR when the user wants the " +
            "protected source. Pass the project's know-how PASSWORD — ask the user for it ONCE in chat if not " +
            "yet known; it is cached per-project on disk (%AppData%/tia-mcp) and the env var TIA_KNOWHOW_PASSWORD " +
            "is also honored, so it is NEVER asked for again. After unlock: formerly-protected blocks are fully " +
            "readable, and future protected blocks are auto-unlocked silently on read. Returns counts; " +
            "passwordLikelyIncorrect=true means the password was wrong; passwordRequired=true means none is " +
            "available yet (ask the user). NOTE: removing protection is a PERMANENT change to the project.")]
        public static async Task<string> KnowHowUnlock(
            OpennessWorkerClient workerClient,
            [Description("The know-how protection password for this project. Ask the user once; it is then cached and never required again.")] string? password = null,
            [Description("Optional PLC name to limit unlocking to one PLC. If omitted, all PLCs are unlocked.")] string? plcName = null,
            [Description("Optional path to a TIA Portal project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.KnowHowUnlockAsync(plcName, password, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
