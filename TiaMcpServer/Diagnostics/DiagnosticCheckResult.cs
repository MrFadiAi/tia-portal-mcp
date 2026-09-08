namespace TiaMcpServer.Diagnostics;

/// <summary>Result of one diagnostic check. <paramref name="Evidence"/> is a list of pre-formatted
/// lines (renderers print them verbatim), so checks own their formatting and renderers stay dumb.</summary>
public sealed record DiagnosticCheckResult(
    string Id,
    DiagnosticStatus Status,
    string Summary,
    IReadOnlyList<string> Evidence)
{
    public static DiagnosticCheckResult Create(
        string id,
        DiagnosticStatus status,
        string summary,
        IEnumerable<string>? evidence = null)
        => new(id, status, summary, evidence is null ? Array.Empty<string>() : evidence.ToList());
}
