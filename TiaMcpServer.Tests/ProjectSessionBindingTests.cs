using TiaMcpServer.Contracts;
using Xunit;

namespace TiaMcpServer.Tests;

public class ProjectSessionBindingTests
{
    [Fact]
    public void FirstExplicitProjectPathBindsSession()
    {
        var binding = new ProjectSessionBinding(null);

        Assert.True(binding.TryResolve("C:\\Projects\\Line.ap21", out var effectivePath, out var error));

        Assert.Equal("C:\\Projects\\Line.ap21", effectivePath);
        Assert.Null(error);
        Assert.Equal("C:\\Projects\\Line.ap21", binding.BoundProjectPath);
    }

    [Fact]
    public void RepeatedSameProjectPathIsAccepted()
    {
        var binding = new ProjectSessionBinding(null);
        binding.TryResolve("C:\\Projects\\Line.ap21", out _, out _);

        Assert.True(binding.TryResolve("C:\\Projects\\Line.ap21", out var effectivePath, out var error));

        Assert.Equal("C:\\Projects\\Line.ap21", effectivePath);
        Assert.Null(error);
    }

    [Fact]
    public void DifferentProjectPathIsRejectedAfterBinding()
    {
        var binding = new ProjectSessionBinding(null);
        binding.TryResolve("C:\\Projects\\Line.ap21", out _, out _);

        Assert.False(binding.TryResolve("C:\\Projects\\Other.ap21", out var effectivePath, out var error));

        Assert.Null(effectivePath);
        Assert.Contains("already bound", error);
    }

    [Fact]
    public void OmittedProjectPathUsesStartupProjectPath()
    {
        var binding = new ProjectSessionBinding("C:\\Projects\\Startup.ap21");

        Assert.True(binding.TryResolve(null, out var effectivePath, out var error));

        Assert.Equal("C:\\Projects\\Startup.ap21", effectivePath);
        Assert.Null(error);
    }
}
