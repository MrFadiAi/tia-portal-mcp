using System;
using Siemens.Engineering;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Compile-as-postcondition for the write paths (adopted from upstream Czarnak's
/// BlockImporter discipline): after a block/type write is imported, compile the written
/// object and verify it re-exports, so a write never silently leaves the project in the
/// UDT-inconsistent state that blocks all subsequent reads. Advisory by design — Openness
/// imports are not transactional, so this NEVER throws or reverts; it appends a loud note
/// (with the compiler errors when they exist) to the write's response.
/// </summary>
internal static class CompilePostcondition
{
    /// <summary>Block-scope verification for update_block_logic: re-resolve the block
    /// (post-import object references are stale), compile just it, verify re-export.</summary>
    public static string VerifyBlock(Project project, string blockPath)
    {
        try
        {
            var address = BlockAddress.Parse(blockPath);
            var target = BlockTargetResolver.ResolveForExport(project, address);
            if (target.Block is null)
            {
                return $"{ConsistencyText.PostconditionPrefix} skipped — the block could not be re-resolved after the import.";
            }

            var result = CompileChecker.CompileObject(target.Block);

            if (result.ErrorCount > 0)
            {
                return ConsistencyText.PostconditionFailureNote(
                    $"Block '{target.Block.Name}'",
                    result.ErrorCount, result.WarningCount,
                    CompileChecker.ErrorTexts(result),
                    reExportVerified: false);
            }

            bool reExportVerified = TryReExport(project, blockPath);
            return ConsistencyText.PostconditionNote(
                $"Block '{target.Block.Name}'",
                result.State.ToString(), result.ErrorCount, result.WarningCount, reExportVerified);
        }
        catch (Exception ex)
        {
            return $"{ConsistencyText.PostconditionPrefix} compile verification could not run: {ex.Message}";
        }
    }

    /// <summary>Software-scope verification for update_type_content: a UDT change silently
    /// invalidates every block that reads its members, so the whole PLC software is
    /// compiled (the compiler is the only thing that knows which dependents broke).</summary>
    public static string VerifySoftware(Project project, string? plcName)
    {
        try
        {
            var report = CompileChecker.Compile(project, plcName, blockPath: null);

            if (report.TotalErrorCount > 0)
            {
                var errors = new System.Collections.Generic.List<string>();
                foreach (var plc in report.Plcs)
                {
                    foreach (var message in plc.Messages)
                    {
                        if (string.Equals(message.Severity, "Error", StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add(message.Path.Length > 0 ? $"{message.Path}: {message.Description}" : message.Description);
                        }
                    }
                }

                return ConsistencyText.PostconditionFailureNote(
                    "the PLC software", report.TotalErrorCount, report.TotalWarningCount, errors, reExportVerified: false);
            }

            return ConsistencyText.PostconditionNote(
                "the PLC software", report.OverallState, report.TotalErrorCount, report.TotalWarningCount, reExportVerified: true);
        }
        catch (Exception ex)
        {
            return $"{ConsistencyText.PostconditionPrefix} compile verification could not run: {ex.Message}";
        }
    }

    private static bool TryReExport(Project project, string blockPath)
    {
        try
        {
            return BlockExporter.Export(project, blockPath, autoHeal: false).Trim().Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
