using TiaMcpServer.Worker;
using Xunit;

namespace TiaMcpServer.Tests;

public class PlcBlockCompareTests
{
    private static BlockInfo B(string name, string type, string source = "") => new() { Name = name, Type = type, Source = source };

    [Fact]
    public void Added_blocks_are_those_only_in_side_a()
    {
        var a = new[] { B("FC1", "FC", "x"), B("FC2", "FC", "y") };
        var b = new[] { B("FC1", "FC", "x") };
        var r = PlcBlockCompare.Compare(a, b);
        Assert.Single(r.Added);
        Assert.Equal("FC2", r.Added[0].Name);
        Assert.Empty(r.Removed);
        Assert.Empty(r.Changed);
        Assert.Single(r.Unchanged);
    }

    [Fact]
    public void Removed_blocks_are_those_only_in_side_b()
    {
        var a = new[] { B("FC1", "FC", "x") };
        var b = new[] { B("FC1", "FC", "x"), B("FB9", "FB", "z") };
        var r = PlcBlockCompare.Compare(a, b);
        Assert.Single(r.Removed);
        Assert.Equal("FB9", r.Removed[0].Name);
    }

    [Fact]
    public void Common_blocks_with_equal_normalized_source_are_unchanged()
    {
        var a = new[] { B("FC1", "FC", "A := 1;\n\n") };
        var b = new[] { B("fc1", "FC", "A := 1;   ") };
        var r = PlcBlockCompare.Compare(a, b);
        Assert.Single(r.Unchanged);
        Assert.Empty(r.Changed);
    }

    [Fact]
    public void Common_blocks_with_different_source_are_changed_and_carry_both_sources()
    {
        var a = new[] { B("FC1", "FC", "A := 1;") };
        var b = new[] { B("FC1", "FC", "A := 2;") };
        var r = PlcBlockCompare.Compare(a, b);
        Assert.Single(r.Changed);
        Assert.Equal("A := 1;", r.Changed[0].SourceA);
        Assert.Equal("A := 2;", r.Changed[0].SourceB);
        Assert.Null(r.Changed[0].Note);
    }

    [Fact]
    public void Same_name_different_type_is_changed_with_type_mismatch_note()
    {
        var a = new[] { B("X1", "FC", "code") };
        var b = new[] { B("X1", "FB", "code") };
        var r = PlcBlockCompare.Compare(a, b);
        Assert.Single(r.Changed);
        Assert.Contains("type-mismatch", r.Changed[0].Note ?? "");
    }
}
