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

    [Fact]
    public void BindSetsUnboundProjectPath()
    {
        var binding = new ProjectSessionBinding(null);

        Assert.True(binding.Bind("C:\\Projects\\Line.ap21", forceRebind: false, out var error));

        Assert.Null(error);
        Assert.Equal("C:\\Projects\\Line.ap21", binding.BoundProjectPath);
    }

    [Fact]
    public void BindRejectsDifferentProjectPathWithoutForce()
    {
        var binding = new ProjectSessionBinding("C:\\Projects\\Line.ap21");

        Assert.False(binding.Bind("C:\\Projects\\Other.ap21", forceRebind: false, out var error));

        Assert.Contains("already bound", error);
        Assert.Equal("C:\\Projects\\Line.ap21", binding.BoundProjectPath);
    }

    [Fact]
    public void BindForceRebindsDifferentProjectPath()
    {
        var binding = new ProjectSessionBinding("C:\\Projects\\Line.ap21");

        Assert.True(binding.Bind("C:\\Projects\\Other.ap21", forceRebind: true, out var error));

        Assert.Null(error);
        Assert.Equal("C:\\Projects\\Other.ap21", binding.BoundProjectPath);
    }

    [Fact]
    public void ClearRemovesMatchingProjectBinding()
    {
        var binding = new ProjectSessionBinding("C:\\Projects\\Line.ap21");

        Assert.True(binding.Clear("C:\\Projects\\Line.ap21", out var error));

        Assert.Null(error);
        Assert.Null(binding.BoundProjectPath);
    }

    [Fact]
    public void ClearRejectsDifferentProjectPath()
    {
        var binding = new ProjectSessionBinding("C:\\Projects\\Line.ap21");

        Assert.False(binding.Clear("C:\\Projects\\Other.ap21", out var error));

        Assert.Contains("already bound", error);
        Assert.Equal("C:\\Projects\\Line.ap21", binding.BoundProjectPath);
    }

    [Fact]
    public void PlcDeviceNameDoesNotAutoBind()
    {
        // PLC device names like "PLF_01A_PLC_SNIJTOOL" should NOT auto-bind
        var binding = new ProjectSessionBinding(null);

        Assert.True(binding.TryResolve("PLF_01A_PLC_SNIJTOOL", out var effectivePath, out var error));

        Assert.Equal("PLF_01A_PLC_SNIJTOOL", effectivePath);
        Assert.Null(error);
        // Session should remain unbound — PLC name is not a project file
        Assert.Null(binding.BoundProjectPath);
    }

    [Fact]
    public void PlcDeviceNameDoesNotBlockLaterProjectBinding()
    {
        // After passing a PLC name, a real project file should still bind
        var binding = new ProjectSessionBinding(null);
        binding.TryResolve("PLF_01A_PLC_SNIJTOOL", out _, out _);

        Assert.True(binding.TryResolve("C:\\Projects\\Line.ap18", out var effectivePath, out var error));

        Assert.Equal("C:\\Projects\\Line.ap18", effectivePath);
        Assert.Null(error);
        Assert.Equal("C:\\Projects\\Line.ap18", binding.BoundProjectPath);
    }

    [Fact]
    public void ProjectFileAutoBinds()
    {
        // Real project files (.ap16, .ap18, .ap19, .ap21) should auto-bind
        var binding = new ProjectSessionBinding(null);

        Assert.True(binding.TryResolve("C:\\Projects\\PLATFORMEN.ap18", out var effectivePath, out var error));

        Assert.Equal("C:\\Projects\\PLATFORMEN.ap18", effectivePath);
        Assert.Null(error);
        Assert.Equal("C:\\Projects\\PLATFORMEN.ap18", binding.BoundProjectPath);
    }

    [Fact]
    public void NonProjectPathDoesNotBlockOtherPaths()
    {
        // Multiple non-project paths should all pass through without binding
        var binding = new ProjectSessionBinding(null);

        Assert.True(binding.TryResolve("PLF_01A_PLC_SNIJTOOL", out _, out _));
        Assert.True(binding.TryResolve("PLF-01A-PLC", out _, out _));
        Assert.Null(binding.BoundProjectPath);

        // Real project file should still bind
        Assert.True(binding.TryResolve("D:\\Projects\\Test.ap21", out _, out _));
        Assert.Equal("D:\\Projects\\Test.ap21", binding.BoundProjectPath);
    }
}
