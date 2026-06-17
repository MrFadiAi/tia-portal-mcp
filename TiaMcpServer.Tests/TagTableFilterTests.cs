using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Contracts;
using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

public class TagTableFilterTests
{
    private static List<TagTableInfo> SampleTables() => new()
    {
        Table("Default tag table", "/"),
        Table("Alpha", "/Group"),
        Table("Beta", "/Group"),
        Table("Gamma", "/A/B"),
    };

    private static TagTableInfo Table(string name, string folder) => new()
    {
        Name = name,
        FolderPath = folder,
        Tags = new List<TagInfo>(),
    };

    // ---- ByFolder ----

    [Fact]
    public void ByFolder_Null_Or_Blank_Returns_All_Unfiltered()
    {
        var tables = SampleTables();

        Assert.Same(tables, TagTableFilter.ByFolder(tables, null));
        Assert.Equal(4, TagTableFilter.ByFolder(tables, "   ").Count);
        Assert.Equal(4, TagTableFilter.ByFolder(tables, "").Count);
    }

    [Fact]
    public void ByFolder_Root_Matches_Only_Root_Tables()
    {
        var result = TagTableFilter.ByFolder(SampleTables(), "/");

        Assert.Single(result);
        Assert.Equal("Default tag table", result[0].Name);
    }

    [Theory]
    [InlineData("/Group")]      // leading slash
    [InlineData("Group")]       // bare
    [InlineData("/Group/")]     // trailing slash
    [InlineData("/group")]      // lower-case
    [InlineData("/GROUP")]      // upper-case
    public void ByFolder_Matches_Folder_Path_And_Case_Insensitive(string folder)
    {
        var result = TagTableFilter.ByFolder(SampleTables(), folder);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Name == "Alpha");
        Assert.Contains(result, t => t.Name == "Beta");
    }

    [Fact]
    public void ByFolder_Nested_Path_Matches()
    {
        var result = TagTableFilter.ByFolder(SampleTables(), "/A/B");

        Assert.Single(result);
        Assert.Equal("Gamma", result[0].Name);
    }

    [Fact]
    public void ByFolder_No_Match_Returns_Empty()
    {
        Assert.Empty(TagTableFilter.ByFolder(SampleTables(), "/NoSuchFolder"));
    }

    // ---- ByName ----

    [Fact]
    public void ByName_Null_Or_Blank_Returns_All_Unfiltered()
    {
        var tables = SampleTables();

        Assert.Same(tables, TagTableFilter.ByName(tables, null));
        Assert.Equal(4, TagTableFilter.ByName(tables, "   ").Count);
    }

    [Theory]
    [InlineData("Alpha")]
    [InlineData("alpha")]   // case-insensitive
    [InlineData("ALPHA")]
    public void ByName_Matches_Case_Insensitive(string name)
    {
        var result = TagTableFilter.ByName(SampleTables(), name);

        Assert.Single(result);
        Assert.Equal("Alpha", result[0].Name);
    }

    [Fact]
    public void ByName_No_Match_Returns_Empty()
    {
        Assert.Empty(TagTableFilter.ByName(SampleTables(), "DoesNotExist"));
    }
}
