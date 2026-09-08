namespace TiaMcpServer.Diagnostics.Checks;

/// <summary>Windows + version. Everything downstream (workers, Openness) requires Windows.</summary>
public sealed class OperatingSystemCheck(IEnvironmentInfo env) : IDiagnosticCheck
{
    public string Id => "os";

    public DiagnosticCheckResult Run()
        => !env.IsWindows
            ? DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Fail,
                $"Not running on Windows ({env.OsVersionDescription}); TIA Portal Openness requires Windows.",
                new[] { $"os: {env.OsVersionDescription}" })
            : DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Ok,
                $"Running on Windows ({env.OsVersionDescription}).",
                new[] { $"os: {env.OsVersionDescription}" });
}
