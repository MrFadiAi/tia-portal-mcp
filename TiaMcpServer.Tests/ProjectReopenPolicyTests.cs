using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// Tests for the project-reopen policy used by the TIA Openness worker.
/// </summary>
/// <remarks>
/// TIA Portal Openness allows only ONE project open per TIA instance. Calling
/// <c>Projects.Open()</c> when a project is already open throws
/// "Another project is already open." So when a project is already open the
/// worker must reuse it instead of reopening it.
/// </remarks>
public class ProjectReopenPolicyTests
{
    [Theory]
    [InlineData(true,  ProjectReopenPolicy.Decision.Reuse)]
    [InlineData(false, ProjectReopenPolicy.Decision.Open)]
    public void DecideReusesOpenProjectInsteadOfReopening(
        bool hasOpenProject,
        ProjectReopenPolicy.Decision expected)
    {
        Assert.Equal(expected, ProjectReopenPolicy.Decide(hasOpenProject));
    }

    [Fact]
    public void DecideNeverReopensWhenAProjectIsAlreadyOpen()
    {
        // Regression guard for the "Another project is already open" failure:
        // once a project is open, the policy must never tell the caller to Open()
        // again (TIA would reject it).
        Assert.Equal(
            ProjectReopenPolicy.Decision.Reuse,
            ProjectReopenPolicy.Decide(hasOpenProject: true));
    }
}
