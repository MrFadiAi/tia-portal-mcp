namespace TiaMcpServer.Diagnostics.Checks;

/// <summary>Membership of the current user in the local "Siemens TIA Openness" group (required
/// for prompt-free attach). Group enumeration failure is a Warn, never a crash.</summary>
public sealed class OpennessGroupCheck(IUserIdentity identity) : IDiagnosticCheck
{
    public const string GroupName = "Siemens TIA Openness";

    public string Id => "openness-group";

    public DiagnosticCheckResult Run()
    {
        var user = identity.UserName;
        var groups = identity.TryGetGroupNames();
        if (groups is null)
        {
            return DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Warn,
                "Could not enumerate the current user's groups — run doctor again as the user the MCP host runs under.",
                new[] { $"user: {user ?? "(unknown)"}" });
        }

        var member = IsOpennessGroupMember(groups);
        var evidence = new[]
        {
            $"user: {user ?? "(unknown)"}",
            $"group: {GroupName}",
            $"member: {member}"
        };

        return member
            ? DiagnosticCheckResult.Create(Id, DiagnosticStatus.Ok, $"User is in the '{GroupName}' group.", evidence)
            : DiagnosticCheckResult.Create(
                Id,
                DiagnosticStatus.Warn,
                $"User is NOT in the '{GroupName}' group — Openness attach will prompt (or fail). Join the group, then sign out/in (group membership only applies to NEW logins).",
                evidence);
    }

    /// <summary>Pure: NTAccount names arrive as "DOMAIN\\name" or "MACHINE\\name" — match on the
    /// group name portion, case-insensitively.</summary>
    internal static bool IsOpennessGroupMember(IEnumerable<string> groupNames)
        => groupNames.Any(g =>
            g.EndsWith(GroupName, StringComparison.OrdinalIgnoreCase) ||
            g.Equals(GroupName, StringComparison.OrdinalIgnoreCase));
}
