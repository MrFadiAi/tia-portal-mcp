using System;
using System.IO;
using TiaMcpServer.Contracts;
using TiaMcpServer.Worker;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// Unit tests for the persistent-worker client's PURE, version-routing logic. OpennessWorkerClient
/// is link-compiled into this assembly (it is Siemens-free), so its internal helpers are testable.
/// These cover the highest-risk untested path: mapping a TIA version to the correct worker exe
/// (a wrong mapping = e.g. a V16 call crashing the V21 worker) and resolving the requested version.
/// </summary>
public class OpennessWorkerClientTests
{
    // -------------------------------------------------- WorkerIdentityForVersion

    [Theory]
    [InlineData(null, "TiaMcpServer.OpennessWorker.exe", "TiaMcpServer.OpennessWorker")]      // auto-detect
    [InlineData(21, "TiaMcpServer.OpennessWorker.exe", "TiaMcpServer.OpennessWorker")]         // V21+ -> standard
    [InlineData(22, "TiaMcpServer.OpennessWorker.exe", "TiaMcpServer.OpennessWorker")]         // future V22 -> standard
    [InlineData(16, "TiaMcpServer.OpennessWorker.V16.exe", "TiaMcpServer.OpennessWorker.V16")] // V16 -> own worker
    [InlineData(17, "TiaMcpServer.OpennessWorker.Legacy.exe", "TiaMcpServer.OpennessWorker.Legacy")] // V17 -> legacy
    [InlineData(18, "TiaMcpServer.OpennessWorker.Legacy.exe", "TiaMcpServer.OpennessWorker.Legacy")] // V18 -> legacy
    [InlineData(19, "TiaMcpServer.OpennessWorker.Legacy.exe", "TiaMcpServer.OpennessWorker.Legacy")] // V19 -> legacy
    [InlineData(20, "TiaMcpServer.OpennessWorker.Legacy.exe", "TiaMcpServer.OpennessWorker.Legacy")] // V20 -> legacy
    public void WorkerIdentityForVersion_Maps_Version_Correctly(int? version, string expectedExe, string expectedDir)
    {
        var (exe, dir) = OpennessWorkerClient.WorkerIdentityForVersion(version);
        Assert.Equal(expectedExe, exe);
        Assert.Equal(expectedDir, dir);
    }

    // -------------------------------------------------- ResolveTiaVersion

    [Fact]
    public void ResolveTiaVersion_Explicit_Request_Wins()
    {
        Assert.Equal(21, OpennessWorkerClient.ResolveTiaVersion(21));
        Assert.Equal(16, OpennessWorkerClient.ResolveTiaVersion(16));
    }

    [Fact]
    public void ResolveTiaVersion_Reads_Version_File_When_Not_Requested()
    {
        var path = Path.Combine(Path.GetTempPath(), "tia-ver-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "  18  "); // trim + parse
        var prev = Environment.GetEnvironmentVariable("TIA_VERSION_FILE");
        try
        {
            Environment.SetEnvironmentVariable("TIA_VERSION_FILE", path);
            Assert.Equal(18, OpennessWorkerClient.ResolveTiaVersion(null));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIA_VERSION_FILE", prev);
            File.Delete(path);
        }
    }

    [Fact]
    public void ResolveTiaVersion_Missing_File_Falls_Back_To_Auto()
    {
        var prev = Environment.GetEnvironmentVariable("TIA_VERSION_FILE");
        try
        {
            Environment.SetEnvironmentVariable("TIA_VERSION_FILE", Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N")));
            Assert.Equal(0, OpennessWorkerClient.ResolveTiaVersion(null)); // 0 = auto-detect
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIA_VERSION_FILE", prev);
        }
    }

    [Fact]
    public void ResolveTiaVersion_No_Env_Var_Falls_Back_To_Auto()
    {
        var prev = Environment.GetEnvironmentVariable("TIA_VERSION_FILE");
        try
        {
            Environment.SetEnvironmentVariable("TIA_VERSION_FILE", null);
            Assert.Equal(0, OpennessWorkerClient.ResolveTiaVersion(null));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIA_VERSION_FILE", prev);
        }
    }

    [Fact]
    public void ResolveTiaVersion_Unparseable_File_Falls_Back_To_Auto()
    {
        var path = Path.Combine(Path.GetTempPath(), "tia-ver-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "V21"); // not a bare integer
        var prev = Environment.GetEnvironmentVariable("TIA_VERSION_FILE");
        try
        {
            Environment.SetEnvironmentVariable("TIA_VERSION_FILE", path);
            Assert.Equal(0, OpennessWorkerClient.ResolveTiaVersion(null));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIA_VERSION_FILE", prev);
            File.Delete(path);
        }
    }

    // -------------------------------------------------- EvaluateStatus (worker_status tool)

    [Fact]
    public void EvaluateStatus_NoWorker_Is_NotAlive_NotStarted()
    {
        var s = OpennessWorkerClient.EvaluateStatus(false, false, false, 0);
        Assert.False(s.IsAlive);
        Assert.False(s.EverStarted);
        Assert.Equal(0, s.TiaVersion);
    }

    [Fact]
    public void EvaluateStatus_LiveWorker_Reports_Version()
    {
        var s = OpennessWorkerClient.EvaluateStatus(true, false, false, 21);
        Assert.True(s.IsAlive);
        Assert.True(s.EverStarted);
        Assert.Equal(21, s.TiaVersion);
    }

    [Theory]
    [InlineData(true, false)]  // flagged dead
    [InlineData(false, true)]  // process exited
    public void EvaluateStatus_Dead_Or_Exited_Is_NotAlive_But_Started(bool isDead, bool hasExited)
    {
        var s = OpennessWorkerClient.EvaluateStatus(true, isDead, hasExited, 18);
        Assert.False(s.IsAlive);
        Assert.True(s.EverStarted); // it was started even if it has since stopped
        Assert.Equal(0, s.TiaVersion); // version not reported when not alive
    }

    [Fact]
    public void GetStatus_With_No_Worker_Started_Reports_NotAlive()
    {
        // The instance method the worker_status MCP tool calls (no TIA interaction).
        var client = new OpennessWorkerClient(new ProjectSessionBinding(null));
        var s = client.GetStatus();
        Assert.False(s.IsAlive);
        Assert.False(s.EverStarted);
    }
}
