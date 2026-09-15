using System;
using System.Collections.Generic;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.SW.Blocks;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// The read-path consistency auto-heal. A block edited in the TIA Portal GUI exports
/// either nothing (UDT-inconsistent) or — worse — the LAST COMPILED source without any
/// error (a plain code edit keeps IsConsistent true; observed live in production chat
/// 0dac593c: "my export read the last compiled version"). Either way the user historically
/// had to compile by hand so the agent could see their edit. This gate (used by
/// get_block_content / read_block_interface, and inherited by read_batch) compiles JUST the
/// requested block first (block-scoped <see cref="CompileChecker.CompileObject"/>, seconds
/// rather than a whole-PLC compile) when its timestamps say it was modified after its last
/// compile, and discloses it via <see cref="ConsistencyText"/>. If the compile reports
/// errors, the failure CARRIES them — the user learns their edit is broken the moment they
/// ask the agent to look.
/// </summary>
internal static class ConsistencyAutoHeal
{
    // One auto-compile per edited state: remembers (per block path) when we last compiled
    // and what the block's CodeModifiedDate was then. If the code has not changed since OUR
    // compile, skip recompiling — this stays correct even when Openness does not bump
    // CompileDate for programmatic compiles (in which case the timestamp gate above would
    // otherwise fire on every read). The worker is a persistent singleton, so the memo
    // lives across calls.
    private static readonly Dictionary<string, (DateTime CompiledAt, DateTime CodeModified)> LastAutoCompile = new();

    /// <summary>
    /// Compile <paramref name="block"/> when it is stale: UDT-inconsistent, or edited after
    /// its last compile (a plain code edit keeps IsConsistent true, and Openness then exports
    /// the LAST COMPILED source without an error — the silent stale read this gate exists to
    /// prevent). Returns null when nothing was needed; throws carrying the compiler errors
    /// when the block does not compile.
    /// </summary>
    public static string? EnsureConsistent(PlcBlock block, string blockPath)
    {
        bool stale;
        DateTime codeModified;
        try
        {
            codeModified = block.CodeModifiedDate;
            stale = ConsistencyText.NeedsCompile(
                block.IsConsistent, codeModified, block.InterfaceModifiedDate, block.CompileDate);
        }
        catch (EngineeringException)
        {
            return null; // cannot tell → let the export attempt; its failure path still hints
        }

        if (!stale)
        {
            return null;
        }

        // Already auto-compiled for exactly this edit state → do not compile again (a second
        // read of the same edited block should not pay the compile cost a second time).
        if (LastAutoCompile.TryGetValue(blockPath, out var memo) && memo.CodeModified == codeModified)
        {
            return null;
        }

        var result = CompileChecker.CompileObject(block);
        LastAutoCompile[blockPath] = (DateTime.Now, codeModified);

        if (result.ErrorCount > 0)
        {
            throw new InvalidOperationException(ConsistencyText.CompileFailureMessage(
                block.Name, result.ErrorCount, result.WarningCount, CompileChecker.ErrorTexts(result)));
        }

        return ConsistencyText.AutoCompiledNote(block.Name, result.State.ToString(), result.WarningCount);
    }
}
