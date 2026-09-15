using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Siemens-free message formatting for the consistency auto-heal feature (the
/// "block is UDT-inconsistent after a TIA Portal edit" state). Pure so the exact
/// agent-facing wording is unit-testable. The READ path auto-compiles a single
/// inconsistent block before exporting it (disclosed via <see cref="AutoCompiledNote"/>);
/// when that compile reports errors the failure carries them (<see cref="CompileFailureMessage"/>)
/// so the user learns their edit does not compile instead of Openness's cryptic
/// "Inconsistent blocks and PLC data types (UDT) cannot be exported".
/// </summary>
public static class ConsistencyText
{
    public const string AutoCompiledPrefix = "[auto-compiled]";
    public const string PostconditionPrefix = "[postcondition]";
    private const int MaxErrorLines = 5;

    /// <summary>Disclosure prepended to a read whose block had to be compiled first. Never silent.</summary>
    public static string AutoCompiledNote(string blockName, string state, int warningCount)
        => $"{AutoCompiledPrefix} Block '{blockName}' was UDT-inconsistent; it was compiled before this read " +
           $"(state: {state}, warnings: {warningCount}).";

    /// <summary>Failure shown when the auto-compile reveals errors — replaces the cryptic export refusal.</summary>
    public static string CompileFailureMessage(string blockName, int errorCount, int warningCount, IReadOnlyList<string> errors)
    {
        var head =
            $"Block '{blockName}' is UDT-inconsistent and DOES NOT COMPILE " +
            $"({errorCount} error(s), {warningCount} warning(s)). Fix these errors in TIA Portal or via update_block_logic, then read again:";

        return head + FormatErrorList(errors);
    }

    /// <summary>One entry of the named skip list search/tag tools emit instead of a bare count.</summary>
    public static string SkippedBlockEntry(string name, bool knowHowProtected, bool inconsistent)
    {
        var reason = knowHowProtected ? "know-how protected"
            : inconsistent ? "UDT-inconsistent — run compile_check to make it searchable"
            : "unreadable (export failed)";
        return $"{name} ({reason})";
    }

    /// <summary>Postcondition note appended to a write response after the write's verification compile.</summary>
    public static string PostconditionNote(string subject, string state, int errorCount, int warningCount, bool reExportVerified)
        => $"{PostconditionPrefix} {subject} compiled after the write: {state}, {errorCount} error(s), {warningCount} warning(s). " +
           (reExportVerified
               ? "Re-export verified — the block is readable exactly as written."
               : "Re-export FAILED — the block may still be inconsistent; run compile_check and retry the read.");

    /// <summary>Loud warning when a write applied but the postcondition compile found errors
    /// (the write stands — Openness imports are not transactional — but the broken state is named).</summary>
    public static string PostconditionFailureNote(string subject, int errorCount, int warningCount, IReadOnlyList<string> errors, bool reExportVerified)
    {
        var head =
            $"{PostconditionPrefix} WARNING: the write APPLIED, but {subject.ToLowerInvariant()} does not compile " +
            $"({errorCount} error(s), {warningCount} warning(s)). The project now contains this broken state — fix and write again:";

        return head + FormatErrorList(errors) +
               (reExportVerified ? string.Empty : "\n(Re-export also failed — expect reads of this object to refuse until it compiles.)");
    }

    private static string FormatErrorList(IReadOnlyList<string> errors)
    {
        var shown = errors.Take(MaxErrorLines).Select(e => $"  - {e}");
        var more = errors.Count > MaxErrorLines ? $"\n  ... and {errors.Count - MaxErrorLines} more" : string.Empty;
        return "\n" + string.Join("\n", shown) + more;
    }
}
