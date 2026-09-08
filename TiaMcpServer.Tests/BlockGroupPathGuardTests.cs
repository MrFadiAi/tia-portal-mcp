using System;
using TiaMcpServer.Contracts;
using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

public class BlockGroupPathGuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RequireRejectsMissingPath(string? groupPath)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => BlockGroupPathGuard.Require(groupPath));
        Assert.Contains("Group path is required", ex.Message);
    }

    [Fact]
    public void RequireRejectsBareGroupName()
    {
        // A bare name is ambiguous across PLCs/units — group management demands the full path.
        var ex = Assert.Throws<InvalidOperationException>(() => BlockGroupPathGuard.Require("MyGroup"));
        Assert.Contains("must name the PLC", ex.Message);
    }

    [Fact]
    public void RequireRejectsLegacyPlcQualifiedName()
    {
        // 'PLC_1/MyGroup' parses as a legacy plc+block address (non-deterministic) — rejected
        // so the caller must spell 'PLC_1/Blocks/MyGroup'.
        var ex = Assert.Throws<InvalidOperationException>(() => BlockGroupPathGuard.Require("PLC_1/MyGroup"));
        Assert.Contains("must name the PLC", ex.Message);
    }

    [Fact]
    public void RequireAcceptsRootChildGroup()
    {
        var address = BlockGroupPathGuard.Require("PLC_1/Blocks/MyGroup");

        Assert.True(address.IsDeterministic);
        Assert.Equal("PLC_1", address.PlcName);
        Assert.Equal("MyGroup", address.BlockName);
        Assert.Empty(address.FolderPath);
        Assert.Equal("PLC_1/Blocks/MyGroup", address.ToDisplayPath());
    }

    [Fact]
    public void RequireAcceptsNestedGroup()
    {
        var address = BlockGroupPathGuard.Require("PLC_1/Blocks/Parent/MyGroup");

        Assert.True(address.IsDeterministic);
        Assert.Equal(new[] { "Parent" }, address.FolderPath);
        Assert.Equal("MyGroup", address.BlockName);
    }

    [Fact]
    public void RequireAcceptsUnitGroup()
    {
        var address = BlockGroupPathGuard.Require("PLC_1/Units/MyUnit/Blocks/MyGroup");

        Assert.True(address.IsDeterministic);
        Assert.True(address.UsesSoftwareUnit);
        Assert.Equal("MyUnit", address.UnitName);
        Assert.Empty(address.FolderPath);
        Assert.Equal("MyGroup", address.BlockName);
    }

    [Fact]
    public void RequireWrapsParseErrors()
    {
        // 'PLC_1/Blocks' alone has no target segment — BlockAddress rejects it; the guard
        // rethrows as InvalidOperationException (the worker's clean-failure contract).
        Assert.Throws<InvalidOperationException>(() => BlockGroupPathGuard.Require("PLC_1/Blocks"));
    }

    [Fact]
    public void ParentDisplayPathTrimsLastSegment()
    {
        Assert.Equal("PLC_1/Blocks/Parent", ParentPathOf("PLC_1/Blocks/Parent/MyGroup"));
        Assert.Equal("PLC_1/Blocks", ParentPathOf("PLC_1/Blocks/MyGroup"));
    }

    private static string ParentPathOf(string groupPath)
        => BlockGroupPathGuard.ParentDisplayPath(BlockGroupPathGuard.Require(groupPath));
}
