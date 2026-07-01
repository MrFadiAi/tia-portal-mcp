namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Policy for deciding whether to reuse an already-open TIA project instead of
/// calling Openness <c>Projects.Open()</c> again. Siemens-free so it can be
/// unit-tested (link-compiled into the test project).
/// </summary>
/// <remarks>
/// TIA Portal Openness permits only ONE project open per TIA instance. Calling
/// <c>Open()</c> when a project is already open throws
/// "Another project is already open." Once a project is open, the worker must
/// therefore reuse it rather than reopen it.
/// </remarks>
public static class ProjectReopenPolicy
{
    /// <summary>What the worker should do with a requested project.</summary>
    public enum Decision
    {
        /// <summary>A project is already open — reuse it; do not call Open().</summary>
        Reuse,

        /// <summary>No project is open — call Open() on the requested path.</summary>
        Open,
    }

    /// <summary>
    /// Decide whether to reuse the open project or open the requested one.
    /// </summary>
    /// <param name="hasOpenProject">
    /// Whether a project is already open in the attached TIA instance.
    /// </param>
    /// <returns>
    /// <see cref="Decision.Reuse"/> if a project is already open; otherwise
    /// <see cref="Decision.Open"/>.
    /// </returns>
    public static Decision Decide(bool hasOpenProject) =>
        hasOpenProject ? Decision.Reuse : Decision.Open;
}
