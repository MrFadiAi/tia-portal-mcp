using System;
using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// Unit tests for <see cref="StlAccessClassifier"/> -- the Siemens-free read/write/comment
/// heuristic extracted from CodeSearcher. Proves tag_usage classifies access correctly and
/// (via <see cref="IsCommentOrHeader"/>) skips comment lines so they are not miscounted.
/// </summary>
public class StlAccessClassifierTests
{
    private static string[] One(params string[] lines) => lines;

    // --------------------------------------------------------------- IsCommentOrHeader

    [Theory]
    [InlineData("// Network 1")]                       // reconstructor network header
    [InlineData("// some comment about AFPAKKER")]     // reconstructed comment
    [InlineData("      //NETWORK TITLE")]              // indented comment (reconstructed form)
    [InlineData("//")]                                 // bare comment marker
    public void IsCommentOrHeader_True_For_Comments_And_Headers(string line)
        => Assert.True(StlAccessClassifier.IsCommentOrHeader(line));

    [Theory]
    [InlineData("      =     \"AFPAKKER\"")]            // code line
    [InlineData("A     \"TAG\"")]
    [InlineData("L     5")]
    [InlineData("CALL \"FC_X\"")]
    [InlineData("")]                                    // blank
    public void IsCommentOrHeader_False_For_Code_And_Blank(string line)
        => Assert.False(StlAccessClassifier.IsCommentOrHeader(line));

    [Fact]
    public void IsCommentOrHeader_Null_Is_False()
        => Assert.False(StlAccessClassifier.IsCommentOrHeader(null!));

    // --------------------------------------------------------------- StartsWriteMnemonic

    [Theory]
    [InlineData("= \"TAG\"", true)]      // assign
    [InlineData("=     \"TAG\"", true)]
    [InlineData("T \"TAG\"", true)]      // transfer
    [InlineData("S \"TAG\"", true)]      // set (flip-flop)
    [InlineData("R \"TAG\"", true)]      // reset
    [InlineData("t \"TAG\"", true)]      // lowercase
    [InlineData("=", true)]              // single char, end of line
    public void StartsWriteMnemonic_True_For_Writes(string s, bool expected)
        => Assert.Equal(expected, StlAccessClassifier.StartsWriteMnemonic(s));

    [Theory]
    [InlineData("SLD 2", false)]         // shift-left-double -- shares 'S', must NOT match
    [InlineData("SRW 1", false)]         // shift-right-word
    [InlineData("SET", false)]           // set RLO (no operand) -- 'E' follows, not whitespace
    [InlineData("RND", false)]           // round -- shares 'R'
    [InlineData("==I 5", false)]         // compare -- starts with '=' but next is '='
    [InlineData("A \"TAG\"", false)]     // read (AND)
    [InlineData("AN \"TAG\"", false)]
    [InlineData("L 5", false)]           // read (load)
    [InlineData("CALL \"FC\"", false)]
    [InlineData("", false)]              // empty
    public void StartsWriteMnemonic_False_For_NonWrites(string s, bool expected)
        => Assert.Equal(expected, StlAccessClassifier.StartsWriteMnemonic(s));

    // --------------------------------------------------------------- IsWriteInstruction

    [Theory]
    [InlineData("Assign", true)]
    [InlineData("Transfer", true)]
    [InlineData("Set", true)]
    [InlineData("Reset", true)]
    [InlineData("=", true)]
    [InlineData("T", true)]
    [InlineData("S", true)]
    [InlineData("R", true)]
    [InlineData("ASSIGN", true)]          // case-insensitive
    [InlineData("A", false)]              // read
    [InlineData("L", false)]
    [InlineData("And", false)]
    [InlineData("CALL", false)]
    public void IsWriteInstruction(string token, bool expected)
        => Assert.Equal(expected, StlAccessClassifier.IsWriteInstruction(token));

    // --------------------------------------------------------------- Classify

    [Fact]
    public void Classify_Reconstructed_Assign_Is_Write()
    {
        var lines = One("// Network 1", "      =     \"AFPAKKER_INSTALLATIE_DRAAIT\"");
        Assert.Equal("write", StlAccessClassifier.Classify(lines, 1, "AFPAKKER_INSTALLATIE_DRAAIT"));
    }

    [Fact]
    public void Classify_Reconstructed_Transfer_Is_Write()
    {
        var lines = One("      T     \"OUT_TAG\"");
        Assert.Equal("write", StlAccessClassifier.Classify(lines, 0, "OUT_TAG"));
    }

    [Fact]
    public void Classify_Reconstructed_And_Is_Read()
    {
        var lines = One("      A     \"IN_TAG\"");
        Assert.Equal("read", StlAccessClassifier.Classify(lines, 0, "IN_TAG"));
    }

    [Fact]
    public void Classify_Reconstructed_Load_Is_Read()
    {
        var lines = One("      L     \"IN_TAG\"");
        Assert.Equal("read", StlAccessClassifier.Classify(lines, 0, "IN_TAG"));
    }

    [Fact]
    public void Classify_Set_RLO_Line_Not_Treated_As_Write()
    {
        // 'SET' (set RLO) shares the 'S' letter but the guard must reject it.
        var lines = One("      SET     \"SOMETAG\"");
        Assert.Equal("read", StlAccessClassifier.Classify(lines, 0, "SOMETAG"));
    }

    [Fact]
    public void Classify_Comparison_Not_Treated_As_Write()
    {
        // '==I' starts with '=' but the guard rejects it (next char '='). A comparison READS.
        var lines = One("      ==I     \"SOMETAG\"");
        Assert.Equal("read", StlAccessClassifier.Classify(lines, 0, "SOMETAG"));
    }

    [Fact]
    public void Classify_Scl_Assignment_Tag_On_Left_Is_Write()
    {
        var lines = One("\"OUT_TAG\" := #temp;");
        Assert.Equal("write", StlAccessClassifier.Classify(lines, 0, "OUT_TAG"));
    }

    [Fact]
    public void Classify_Scl_Assignment_Tag_On_Right_Is_Not_Write()
    {
        // #temp := "OUT_TAG" -- tag is the SOURCE (right of :=), so this is a READ of OUT_TAG.
        var lines = One("#temp := \"OUT_TAG\";");
        Assert.Equal("read", StlAccessClassifier.Classify(lines, 0, "OUT_TAG"));
    }

    [Fact]
    public void Classify_Raw_Xml_Assign_Token_Lookback_Is_Write()
    {
        // Reconstructed path is gone for raw XML; the StlToken lookback must classify it.
        var lines = One(
            "      <StlStatement>",
            "        <StlToken Text=\"Assign\"/>",
            "        <Access Scope=\"GlobalVariable\"><Symbol><Component Name=\"MYTAG\"/></Symbol></Access>");
        Assert.Equal("write", StlAccessClassifier.Classify(lines, 2, "MYTAG"));
    }

    [Fact]
    public void Classify_Raw_Xml_And_Token_Lookback_Is_Read()
    {
        var lines = One(
            "        <StlToken Text=\"A\"/>",
            "        <Access Scope=\"GlobalVariable\"><Symbol><Component Name=\"MYTAG\"/></Symbol></Access>");
        Assert.Equal("read", StlAccessClassifier.Classify(lines, 1, "MYTAG"));
    }

    [Fact]
    public void Classify_Raw_Xml_Operand_No_Token_Is_Unknown()
    {
        // Operand line with no recoverable StlToken above it -> do not guess "read".
        var lines = One(
            "// Network 1",
            "        <Access Scope=\"GlobalVariable\"><Symbol><Component Name=\"ORPHAN\"/></Symbol></Access>");
        Assert.Equal("unknown", StlAccessClassifier.Classify(lines, 1, "ORPHAN"));
    }
}
