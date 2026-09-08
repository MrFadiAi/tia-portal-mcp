namespace TiaMcpServer.Diagnostics.Checks;

/// <summary>.NET Framework 4.8 for the net48 Openness workers (registry Release >= 528040).</summary>
public sealed class DotNetFrameworkCheck(IRegistryReader registry, IEnvironmentInfo env) : IDiagnosticCheck
{
    private const string FullKey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full";
    private const string ReleaseValue = "Release";
    private const int MinimumRelease = 528040; // .NET Framework 4.8

    public string Id => "dotnet-framework";

    public DiagnosticCheckResult Run()
    {
        if (!env.IsWindows)
        {
            return DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Fail,
                "Not running on Windows; .NET Framework 4.8 cannot be detected.",
                new[] { $"registry: HKLM\\{FullKey}" });
        }

        var release = registry.GetLocalMachineInt64(FullKey, ReleaseValue);
        var evidence = new[]
        {
            $"registry: HKLM\\{FullKey}\\{ReleaseValue}",
            $"release: {release?.ToString() ?? "(not found)"}"
        };

        if (release is null)
        {
            return DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Fail,
                ".NET Framework 4.8 was not detected — the net48 Openness workers require it.",
                evidence);
        }

        return release < MinimumRelease
            ? DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Fail,
                $".NET Framework release {release} is below 4.8 ({MinimumRelease}) — the net48 Openness workers require 4.8 or newer.",
                evidence)
            : DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Ok,
                $".NET Framework 4.8+ detected (release {release}).",
                evidence);
    }
}
