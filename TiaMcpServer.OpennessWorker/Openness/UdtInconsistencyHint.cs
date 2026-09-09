using System;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Classifies the TIA Openness "UDT-inconsistent block" export failure and builds the
/// recovery instruction appended to read-tool errors. Pure (Siemens-free) so the
/// classification is unit-testable against the real message text. A UDT-inconsistent block
/// (its interface references a PLC data type whose compiled state is out of date) cannot be
/// exported at all until its PLC is compiled again — compiling regenerates the block, after
/// which get_block_content / read_block_interface work. The READ path never auto-compiles
/// (compile is a mutation): the instruction tells the agent to run compile_check as a
/// visible step and then retry.
/// </summary>
public static class UdtInconsistencyHint
{
    /// <summary>
    /// True for the inconsistency class of export failures, e.g. "Inconsistent blocks and PLC
    /// data types (UDT) cannot be exported". Requires the inconsistency signal PLUS an
    /// export/UDT signal, so generic "project is in an inconsistent state" notes and
    /// know-how-protection errors do NOT match.
    /// </summary>
    public static bool IsUdtInconsistency(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var text = message.ToLowerInvariant();
        var inconsistent = text.Contains("inconsistent");
        var export = text.Contains("cannot be exported")
                     || text.Contains("could not be exported")
                     || text.Contains("not exported");
        var udt = text.Contains("udt") || text.Contains("data type");
        return inconsistent && (export || udt);
    }

    /// <summary>The recovery instruction (exact wording the read tools append). Placeholders
    /// are filled with the best-known PLC name and TIA version.</summary>
    public static string Build(string? plcName, int? tiaVersion)
        => "This block is UDT-inconsistent. Run compile_check on this PLC (plcName="
           + (string.IsNullOrWhiteSpace(plcName) ? "the PLC that owns this block" : plcName)
           + ", tiaVersion=" + (tiaVersion?.ToString() ?? "auto-detect")
           + "), then retry this read — compiling regenerates the block.";

    /// <summary>Append the recovery instruction when (and only when) the error message is the
    /// UDT-inconsistency class; other messages pass through unchanged.</summary>
    public static string Append(string message, string? plcName, int? tiaVersion)
        => IsUdtInconsistency(message)
            ? message + "\n\n" + Build(plcName, tiaVersion)
            : message;
}
