using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class WorkerStatusTool
{
    [McpServerTool(Name = "worker_status")]
    [Description(
        "Report whether the TIA Openness worker process is alive, which TIA version it was " +
        "spawned for, and whether it has ever been started. Use this to diagnose slowness or " +
        "errors (a dead worker is auto-respawned on the next call, so 'not alive' just means " +
        "no call has run since the last exit/timeout). Does not touch TIA Portal.")]
    public static string Status(OpennessWorkerClient workerClient)
    {
        var s = workerClient.GetStatus();
        var state = s.IsAlive ? "alive" : s.EverStarted ? "stopped (will respawn on next call)" : "not started";
        var version = s.TiaVersion == 0 ? "auto-detect" : $"V{s.TiaVersion}";
        return
            $"TIA Openness worker: {state}\n" +
            $"TIA version: {version}\n" +
            $"Ever started: {s.EverStarted}";
    }
}
