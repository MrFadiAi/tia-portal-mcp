using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class BlockExporter
{
    public static string Export(Project project, string blockPath, string? projectPath = null, bool raw = false, bool autoHeal = false, bool quietHeal = false)
    {
        var address = BlockAddress.Parse(blockPath);
        var target = BlockTargetResolver.ResolveForExport(project, address);

        // Stale-read auto-heal: a block edited in the TIA Portal GUI is either
        // UDT-inconsistent (export refuses) or — for a plain code edit — silently exports
        // the LAST COMPILED source. With autoHeal (single-block chat reads — also the
        // read_batch route), compile JUST this block first when its timestamps say it was
        // modified after its last compile, and disclose it in the source header; compile
        // errors surface as a clear failure listing them. Bulk callers (extract_plc_blocks /
        // compare) pass autoHeal:false — compiling a whole PLC from a bulk read would be a
        // surprise; the roster's isConsistent flags disclose instead. raw (diagnostic) mode
        // never heals: side-effect-free by contract.
        // quietHeal: compile stale blocks WITHOUT the in-band [auto-compiled] note — used by
        // bulk extraction (compare): the note line would itself show up as a diff.
        string? healNote = autoHeal && !raw
            ? ConsistencyAutoHeal.EnsureConsistent(target.Block!, blockPath)
            : null;
        if (quietHeal)
        {
            healNote = null;
        }

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
            var exportedLegacy = File.ReadAllText(exportPath);
            // raw = diagnostic bypass: return the tokenized XML unchanged (see WorkerRequest.Raw).
            var source = raw ? exportedLegacy : BlockSourceReconstructor.Reconstruct(exportedLegacy, target.Block!.ProgrammingLanguage.ToString());
            return PrefixNote(source, healNote);
#else
            // DB blocks must take the XML route, NOT ExportAsDocuments: the documents API
            // succeeds for DBs but emits SIMATIC-SD text the DB reconstructor cannot parse,
            // silently degrading to the empty "DATA_BLOCK/STRUCT/END_STRUCT" stub — which made
            // every V21 DB compare as changed-and-empty against V18's full listing (observed
            // live: "DATA_BLOCK "DATA ANALOOG" / DB 901" vs a bare stub the user read as a
            // false difference). XML keeps both versions on the identical reconstruction path.
            var isDb = string.Equals(target.Block!.ProgrammingLanguage.ToString(), "DB", StringComparison.OrdinalIgnoreCase);
            string? combined;
            if (!raw && isDb)
            {
                combined = TryExportToFile(target.Block!, tempDir, target.DocumentName);
            }
            else
            {
                combined = TryExportAsDocuments(target.Block!, tempDir, target.DocumentName)
                    ?? TryExportToFile(target.Block!, tempDir, target.DocumentName);
            }

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
            // only; other languages pass through unchanged. raw = diagnostic bypass: return the
            // tokenized XML unchanged so we can see why reconstruction drops content.
            var source = raw ? combined : BlockSourceReconstructor.Reconstruct(combined, target.Block!.ProgrammingLanguage.ToString());
            return PrefixNote(source, healNote);
#endif
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>The auto-compile disclosure rides as a leading comment line inside the
    /// returned source (get_block_content returns plain code text, so the note must live
    /// in-band — and it must survive into the content hash so the re-read cache treats a
    /// healed read as different from a clean one).</summary>
    private static string PrefixNote(string source, string? note)
        => note is null ? source : "// " + note + "\n" + source;

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
