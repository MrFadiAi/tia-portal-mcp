namespace TiaMcpServer.Diagnostics.Checks;

/// <summary>The .NET 8 host runtime. (The net48 workers are covered by dotnet-framework.)</summary>
public sealed class DotNetRuntimeCheck(IEnvironmentInfo env) : IDiagnosticCheck
{
    private const int RequiredMajor = 8;

    public string Id => "dotnet-runtime";

    public DiagnosticCheckResult Run()
    {
        var version = env.RuntimeVersion;
        if (Version.TryParse(version, out var parsed) && parsed.Major >= RequiredMajor)
        {
            return DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Ok,
                $".NET {parsed.Major} runtime in use (>= {RequiredMajor} required).",
                new[] { $"runtime: {version}" });
        }

        return DiagnosticCheckResult.Create(
            Id,
            DiagnosticStatus.Fail,
            $".NET runtime {version} is older than the required .NET {RequiredMajor}.",
            new[] { $"runtime: {version}" });
    }
}
