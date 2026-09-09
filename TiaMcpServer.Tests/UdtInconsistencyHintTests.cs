using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// Unit tests for the pure UDT-inconsistency classifier + recovery instruction: given an
/// export-failure message, decide whether it is the "block references an out-of-date PLC
/// data type" class and, if so, append the exact compile_check-and-retry instruction. Read
/// paths never auto-compile — the instruction is the entire recovery surface.
/// </summary>
public class UdtInconsistencyHintTests
{
    // verbatim from live chat 4c263cd9 (read_block_interface PUTBAND)
    private const string LiveMessage =
        "Inconsistent blocks and PLC data types (UDT) cannot be exported";

    [Fact]
    public void Matches_The_Live_Chat_Message()
    {
        Assert.True(UdtInconsistencyHint.IsUdtInconsistency(LiveMessage));
    }

    [Theory]
    [InlineData("Inconsistent blocks and PLC data types (UDT) cannot be exported")]
    [InlineData("The block 'PUTBAND' is inconsistent and cannot be exported.")]
    [InlineData("inconsistent data type — block not exported")]
    [InlineData("EngineeringException: INCONSISTENT BLOCKS AND PLC DATA TYPES (UDT) CANNOT BE EXPORTED")]
    public void Matches_Wording_Variants_Case_Insensitively(string message)
    {
        Assert.True(UdtInconsistencyHint.IsUdtInconsistency(message));
    }

    [Theory]
    [InlineData("The block is know-how-protected and cannot be exported.")] // protected, not inconsistent
    [InlineData("The project may be in an inconsistent state.")]             // no export/UDT signal
    [InlineData("Block 'FC100' not found.")]
    [InlineData("The file is locked by another process.")]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_Non_Inconsistency_Messages(string message)
    {
        Assert.False(UdtInconsistencyHint.IsUdtInconsistency(message));
    }

    [Fact]
    public void Rejects_Null_Message()
    {
        Assert.False(UdtInconsistencyHint.IsUdtInconsistency(null));
    }

    [Fact]
    public void Build_Produces_The_Exact_Instruction_Text()
    {
        var instruction = UdtInconsistencyHint.Build("PLF-01A-PLC_9", 21);

        Assert.Equal(
            "This block is UDT-inconsistent. Run compile_check on this PLC (plcName=PLF-01A-PLC_9, tiaVersion=21), " +
            "then retry this read — compiling regenerates the block.",
            instruction);
    }

    [Fact]
    public void Build_Falls_Back_To_Guiding_Placeholders_When_Plc_And_Version_Are_Unknown()
    {
        var instruction = UdtInconsistencyHint.Build(null, null);

        Assert.Contains("plcName=the PLC that owns this block", instruction);
        Assert.Contains("tiaVersion=auto-detect", instruction);
        Assert.EndsWith("then retry this read — compiling regenerates the block.", instruction);
    }

    [Fact]
    public void Append_Adds_The_Hint_Only_For_The_Inconsistency_Class()
    {
        var appended = UdtInconsistencyHint.Append(LiveMessage, "PLF-01A-PLC_9", 21);
        var untouched = UdtInconsistencyHint.Append("The block is know-how-protected and cannot be exported.", "X", 21);

        Assert.Equal(LiveMessage + "\n\n" + UdtInconsistencyHint.Build("PLF-01A-PLC_9", 21), appended);
        Assert.Equal("The block is know-how-protected and cannot be exported.", untouched);
    }
}
