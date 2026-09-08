using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Diagnostics;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class DoctorTool
{
    [McpServerTool(Name = "doctor")]
    [Description(
        "Self-diagnosis for the TIA Portal Openness environment — answers even when the TIA tools " +
        "themselves are broken. Checks (host-side only, no TIA interaction, no Siemens DLLs): os, " +
        "dotnet-runtime, dotnet-framework (4.8 for the net48 workers), tia-installations (V16/V18/V21 " +
        "registry + PublicAPI assemblies), openness-group (user in 'Siemens TIA Openness'), whitelist " +
        "(the Business-access approval: does the registry Entry's FileHash match the CURRENT exe " +
        "SHA-256 — detects rebuilt-but-not-re-approved workers), worker-binaries (three version-routed " +
        "workers exist and are not older than the host exe — detects copy-locked stale builds), " +
        "tia-processes (is a TIA Portal running), host-worker-version. Returns per-check status " +
        "(ok/info/warn/fail), evidence, and a summary; pass format:'json' for machine-readable output.")]
    public static string RunDoctor(string? format = null)
    {
        var report = DoctorFactory.CreateDefault().Run();
        return string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)
            ? DoctorJsonRenderer.Render(report)
            : DoctorTextRenderer.Render(report);
    }
}
