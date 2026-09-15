using System;
using System.Collections.Generic;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.SW.Blocks;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// The read-path consistency auto-heal. A block edited in the TIA Portal GUI is left
/// UDT-inconsistent and Openness refuses to export it until a compile — historically the
/// user had to compile by hand so the agent could read the block back. This gate (used by
/// get_block_content / read_block_interface, and inherited by read_batch) compiles JUST the
/// requested block first (block-scoped <see cref="CompileChecker.CompileObject"/>, seconds
/// rather than a whole-PLC compile) and discloses it via <see cref="ConsistencyText"/>.
/// If the compile reports errors, the failure CARRIES them — the user learns their edit is
/// broken the moment they ask the agent to look, instead of Openness's cryptic
/// "Inconsistent blocks and PLC data types (UDT) cannot be exported".
/// </summary>
internal static class ConsistencyAutoHeal
{
    /// <summary>
    /// Compile <paramref name="block"/> when it is UDT-inconsistent. Returns null when
    /// nothing was needed (block already consistent, or its state could not be read — the
    /// export attempt then still has the <see cref="UdtInconsistencyHint"/> fallback).
    /// Throws <see cref="InvalidOperationException"/> carrying the compiler errors when the
    /// block does not compile.
    /// </summary>
    public static string? EnsureConsistent(PlcBlock block)
    {
        bool inconsistent;
        try
        {
            inconsistent = !block.IsConsistent;
        }
        catch (EngineeringException)
        {
            return null; // cannot tell → let the export attempt; its failure path still hints
        }

        if (!inconsistent)
        {
            return null;
        }

        var result = CompileChecker.CompileObject(block);

        if (result.ErrorCount > 0)
        {
            throw new InvalidOperationException(ConsistencyText.CompileFailureMessage(
                block.Name, result.ErrorCount, result.WarningCount, CompileChecker.ErrorTexts(result)));
        }

        return ConsistencyText.AutoCompiledNote(block.Name, result.State.ToString(), result.WarningCount);
    }
}
