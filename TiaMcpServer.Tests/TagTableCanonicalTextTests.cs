using System.Collections.Generic;
using TiaMcpServer.Contracts;
using TiaMcpServer.Worker;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// The canonical tag-table text is what compare_plc_blocks diffs for the tags section —
/// the tests pin: one line per tag/constant, name-sorted (a TIA reorder is NOT a change),
/// and that two tables differing in exactly one field produce exactly one diff line when
/// run through the shared comparer's NormalizeSource.
/// </summary>
public class TagTableCanonicalTextTests
{
    private static TagTableInfo Table(
        string name = "Default tag table",
        string folder = "/",
        bool isDefault = false,
        List<TagInfo>? tags = null,
        List<UserConstantInfo>? constants = null)
        => new()
        {
            Name = name,
            FolderPath = folder,
            IsDefault = isDefault,
            Tags = tags ?? new List<TagInfo>(),
            UserConstants = constants ?? new List<UserConstantInfo>(),
        };

    [Fact]
    public void Renders_One_Line_Per_Tag_With_Type_Address_And_Comment()
    {
        var text = TagTableCanonicalText.Render(Table(tags: new List<TagInfo>
        {
            new() { Name = "VOETJESAFVOER RUNNING", DataType = "Bool", LogicalAddress = "%I43.4", Comment = "field feedback" },
        }));

        Assert.Contains("TABLE \"Default tag table\" (/)", text);
        Assert.Contains("TAG \"VOETJESAFVOER RUNNING\" : Bool := %I43.4  // field feedback", text);
    }

    [Fact]
    public void Sorts_Tags_And_Constants_By_Name_So_A_Reorder_Is_Not_A_Change()
    {
        var a = TagTableCanonicalText.Render(Table(tags: new List<TagInfo>
        {
            new() { Name = "B", DataType = "Bool", LogicalAddress = "%I0.0" },
            new() { Name = "A", DataType = "Bool", LogicalAddress = "%I0.1" },
        }));
        var b = TagTableCanonicalText.Render(Table(tags: new List<TagInfo>
        {
            new() { Name = "A", DataType = "Bool", LogicalAddress = "%I0.1" },
            new() { Name = "B", DataType = "Bool", LogicalAddress = "%I0.0" },
        }));

        Assert.Equal(PlcBlockCompare.NormalizeSource(a), PlcBlockCompare.NormalizeSource(b));
    }

    [Fact]
    public void Renders_User_Constants()
    {
        var text = TagTableCanonicalText.Render(Table(constants: new List<UserConstantInfo>
        {
            new() { Name = "MAX_SNELHEID", DataType = "Int", Value = "1720" },
        }));

        Assert.Contains("CONSTANT \"MAX_SNELHEID\" : Int := 1720", text);
    }

    [Fact]
    public void Foldered_Table_Key_Carries_Its_Path()
    {
        var text = TagTableCanonicalText.Render(Table(name: "KRAT", folder: "/Station 3"));

        Assert.Contains("(/Station 3)", text);
    }

    [Fact]
    public void Changed_Address_Produces_Exactly_One_Diff_Line_Pair()
    {
        var a = PlcBlockCompare.NormalizeSource(TagTableCanonicalText.Render(Table(tags: new List<TagInfo>
        {
            new() { Name = "A", DataType = "Bool", LogicalAddress = "%I0.0" },
            new() { Name = "B", DataType = "Bool", LogicalAddress = "%I0.1" },
        })));
        var b = PlcBlockCompare.NormalizeSource(TagTableCanonicalText.Render(Table(tags: new List<TagInfo>
        {
            new() { Name = "A", DataType = "Bool", LogicalAddress = "%I0.7" },
            new() { Name = "B", DataType = "Bool", LogicalAddress = "%I0.1" },
        })));

        Assert.NotEqual(a, b);
        var aLines = a.Split('\n');
        var bLines = b.Split('\n');
        var changed = 0;
        for (var i = 0; i < aLines.Length && i < bLines.Length; i++)
        {
            if (aLines[i] != bLines[i]) changed++;
        }
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Comment_Only_Difference_Counts_As_Changed()
    {
        var a = TagTableCanonicalText.Render(Table(tags: new List<TagInfo>
        {
            new() { Name = "A", DataType = "Bool", LogicalAddress = "%I0.0", Comment = "old" },
        }));
        var b = TagTableCanonicalText.Render(Table(tags: new List<TagInfo>
        {
            new() { Name = "A", DataType = "Bool", LogicalAddress = "%I0.0", Comment = "new" },
        }));

        Assert.NotEqual(a, b);
    }
}
