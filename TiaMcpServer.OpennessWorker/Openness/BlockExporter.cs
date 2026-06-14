using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class BlockExporter
{
    public static string Export(Project project, string blockPath, string? projectPath = null)
    {
        var address = BlockAddress.Parse(blockPath);
        var target = BlockTargetResolver.ResolveForExport(project, address);

        string tempDir = Path.Combine(Path.GetTempPath(), "tia-mcp-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Delete existing files before export (V21 requirement, harmless on V16-V19)
            if (Directory.Exists(tempDir))
            {
                foreach (var existingFile in Directory.GetFiles(tempDir))
                {
                    try { File.Delete(existingFile); }
                    catch { /* ignore */ }
                }
            }

#if LEGACY_TIA
            // V16-V18: Use legacy Export API (XML export)
            var exportPath = Path.Combine(tempDir, target.DocumentName);
            target.Block!.Export(new FileInfo(exportPath), ExportOptions.WithDefaults);
            return BlockSourceReconstructor.Reconstruct(File.ReadAllText(exportPath), target.Block!.ProgrammingLanguage.ToString());
#else
            var combined = TryExportAsDocuments(target.Block!, tempDir, target.DocumentName)
                ?? TryExportToFile(target.Block!, tempDir, target.DocumentName);

            // Still nothing → likely know-how protected. Auto-unlock with a cached password and retry once.
            if (string.IsNullOrEmpty(combined) && KnowHowAutoUnlock.TryUnprotect(target.Block!, projectPath))
            {
                combined = TryExportToFile(target.Block!, tempDir, target.DocumentName);
            }

            if (string.IsNullOrEmpty(combined))
            {
                throw new InvalidOperationException("Block export produced no content (block may be know-how-protected — provide the password via knowhow_unlock).");
            }

            // Reconstruct readable STL from the tokenized XML so get_block_content returns code
            // (e.g. '      T     "PLUKSCHIJF"') instead of raw <StlToken>/<Component> XML. STL
            // only; other languages pass through unchanged.
            return BlockSourceReconstructor.Reconstruct(combined, target.Block!.ProgrammingLanguage.ToString());
#endif
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

#if !LEGACY_TIA
    /// <summary>Try the preferred V21 ExportAsDocuments API. Returns the concatenated
    /// document text, or null if it failed / produced nothing (caller falls back).</summary>
    private static string? TryExportAsDocuments(PlcBlock block, string tempDir, string documentName)
    {
        try
        {
            var result = block.ExportAsDocuments(new DirectoryInfo(tempDir), documentName);
            if (result.State != DocumentResultState.Success)
                return null;

            var combined = new StringBuilder();
            foreach (FileInfo file in result.ExportedDocuments)
            {
                combined.Append($"--- FILE: {file.Name} ---\n");
                combined.Append(File.ReadAllText(file.FullName));
            }

            return combined.Length == 0 ? null : combined.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Export via Export(FileInfo, ExportOptions) — the reliable fallback the GUI
    /// exporter (tia_export_blocks.cs) uses on V21. Returns null on failure.</summary>
    private static string? TryExportToFile(PlcBlock block, string tempDir, string documentName)
    {
        try
        {
            var path = Path.Combine(tempDir, documentName + ".xml");
            block.Export(new FileInfo(path), ExportOptions.WithDefaults);
            return File.ReadAllText(path);
        }
        catch
        {
            return null;
        }
    }
#endif
}
