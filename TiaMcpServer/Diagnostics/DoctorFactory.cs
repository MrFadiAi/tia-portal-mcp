namespace TiaMcpServer.Diagnostics;

/// <summary>Composition root for the doctor: real system interfaces + all checks. Both the MCP
/// tool and the `doctor` CLI branch use this, so they can never drift apart.</summary>
public static class DoctorFactory
{
    public static DoctorRunner CreateDefault()
    {
        var env = new EnvironmentInfo();
        var registry = new WindowsRegistryReader();
        var fileSystem = new RealFileSystemProbe();
        var repoRoot = FindRepoRoot(env.BaseDirectory, fileSystem) ?? env.BaseDirectory;
        var hostExePath = env.HostExePath ?? Path.Combine(env.BaseDirectory, "TiaMcpServer.exe");

        var checks = new IDiagnosticCheck[]
        {
            new Checks.OperatingSystemCheck(env),
            new Checks.DotNetRuntimeCheck(env),
            new Checks.DotNetFrameworkCheck(registry, env),
            new Checks.TiaPortalInstallationCheck(registry, fileSystem),
            new Checks.OpennessGroupCheck(new CurrentUserIdentity()),
            new Checks.WhitelistCheck(registry, fileSystem, repoRoot, Path.GetDirectoryName(hostExePath)),
            new Checks.WorkerBinariesCheck(fileSystem, repoRoot, hostExePath),
            new Checks.TiaPortalProcessCheck(new SystemProcessLister()),
            new Checks.HostWorkerVersionCheck(env, fileSystem, repoRoot)
        };

        return new DoctorRunner(env.HostVersion, checks);
    }

    /// <summary>Walk up from <paramref name="startDirectory"/> to the directory holding the
    /// solution marker (TiaMcpServer.sln or .git). Null at the drive root. Pure given the probe,
    /// so the walk is unit-testable.</summary>
    internal static string? FindRepoRoot(string startDirectory, IFileSystemProbe fileSystem)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            if (fileSystem.FileExists(Path.Combine(dir.FullName, "TiaMcpServer.sln")) ||
                fileSystem.DirectoryExists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }

            dir = dir.Parent; // null at the drive root -> loop ends
        }

        return null;
    }
}
