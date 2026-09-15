using System.Collections.Generic;
using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// The exact wording of the auto-compile disclosures — these strings are what the agent
/// (and through it, the user) sees, so the shape is pinned: the disclosure must NAME the
/// block and state, the compile failure must LIST the compiler errors (it replaces
/// Openness's cryptic "Inconsistent blocks and PLC data types (UDT) cannot be exported"),
/// and the skip list must give a per-block REASON, not a bare count.
/// </summary>
public class ConsistencyTextTests
{
    [Fact]
    public void AutoCompiledNote_Names_Block_State_And_Warnings()
    {
        var note = ConsistencyText.AutoCompiledNote("PUTBAND", "Success", 2);

        Assert.StartsWith(ConsistencyText.AutoCompiledPrefix, note);
        Assert.Contains("'PUTBAND'", note);
        Assert.Contains("UDT-inconsistent", note);
        Assert.Contains("state: Success", note);
        Assert.Contains("warnings: 2", note);
    }

    [Fact]
    public void CompileFailureMessage_Lists_Up_To_Five_Errors_Then_Counts_The_Rest()
    {
        var errors = new List<string>
        {
            "HOOFDPROGRAMMA: undeclared identifier 'FOO'",
            "FC100: syntax error line 42",
            "FC101: type mismatch",
            "FC102: missing END_IF",
            "FC103: unknown instruction",
            "FC104: unknown instruction",
            "FC105: unknown instruction",
        };

        var message = ConsistencyText.CompileFailureMessage("PUTBAND", 7, 1, errors);

        Assert.Contains("DOES NOT COMPILE", message);
        Assert.Contains("7 error(s), 1 warning(s)", message);
        Assert.Equal(5, CountOccurrences(message, "  - "));
        Assert.Contains("... and 2 more", message);
    }

    [Fact]
    public void CompileFailureMessage_With_No_Error_Texts_Still_Explains_The_State()
    {
        var message = ConsistencyText.CompileFailureMessage("OB1", 1, 0, new List<string>());

        Assert.Contains("DOES NOT COMPILE", message);
        Assert.Contains("1 error(s)", message);
    }

    [Theory]
    [InlineData(true, false, "know-how protected")]
    [InlineData(false, true, "UDT-inconsistent — run compile_check")]
    [InlineData(false, false, "unreadable (export failed)")]
    public void SkippedBlockEntry_Gives_Per_Block_Reason(bool knowHow, bool inconsistent, string expectedReason)
    {
        var entry = ConsistencyText.SkippedBlockEntry("FC_COMM_MYC_->CHR", knowHow, inconsistent);

        Assert.StartsWith("FC_COMM_MYC_->CHR (", entry);
        Assert.Contains(expectedReason, entry);
    }

    [Fact]
    public void PostconditionNote_Reports_State_And_ReExport_Verdict()
    {
        var ok = ConsistencyText.PostconditionNote("Block 'FC100'", "Success", 0, 1, reExportVerified: true);

        Assert.StartsWith(ConsistencyText.PostconditionPrefix, ok);
        Assert.Contains("Block 'FC100' compiled after the write", ok);
        Assert.Contains("0 error(s), 1 warning(s)", ok);
        Assert.Contains("readable exactly as written", ok);
    }

    [Fact]
    public void PostconditionNote_Failed_ReExport_Warns_About_The_Consequence()
    {
        var note = ConsistencyText.PostconditionNote("Block 'FC100'", "Success", 0, 0, reExportVerified: false);

        Assert.Contains("Re-export FAILED", note);
        Assert.Contains("compile_check", note);
    }

    [Fact]
    public void PostconditionFailureNote_Is_Loud_And_Lists_Errors()
    {
        var note = ConsistencyText.PostconditionFailureNote(
            "Block 'FC100'", 2, 0,
            new List<string> { "line 10: undeclared identifier 'X'", "line 20: type mismatch" },
            reExportVerified: false);

        Assert.Contains("WARNING: the write APPLIED", note);
        Assert.Contains("does not compile", note);
        Assert.Contains("  - line 10: undeclared identifier 'X'", note);
        Assert.Contains("expect reads of this object to refuse", note);
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
