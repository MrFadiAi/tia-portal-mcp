using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Builds an in-memory index of PLC block source code (one entry per unprotected
/// block, with its code split into lines). Powers search_code and tag_usage so
/// "where is X used" questions are answered in one pass instead of block-by-block.
/// Know-how-protected blocks cannot be exported and are counted as skipped.
/// </summary>
internal static class BlockCodeIndexer
{
    /// <summary>Safety cap so a huge project can't exhaust memory/time in one call.</summary>
    private const int MaxBlocks = 2000;

    public static IndexBuildResult Build(Project project, string? plcNameFilter, string? projectPath)
    {
        var blocks = new List<IndexedBlock>();
        var skipped = 0;
        var total = 0;

        foreach (var (_, plc) in PlcSoftwareFinder.Filter(project, plcNameFilter))
        {
            if (blocks.Count >= MaxBlocks)
            {
                break;
            }

            WalkBlockGroup(plc.BlockGroup, plc.Name, projectPath, blocks, ref skipped, ref total);
        }

        return new IndexBuildResult(blocks, skipped, total);
    }

    /// <summary>
    /// Cached entry point for search_code / tag_usage / hmi_tag_trace. Builds the index once
    /// per (projectPath, plcName) and reuses it until a mutation clears the cache or the TTL
    /// expires — so three calls in one chat export all blocks ONCE, not three times. Delegates
    /// the actual export to <see cref="Build"/>.
    /// </summary>
    public static IndexBuildResult GetOrBuild(Project project, string? projectPath, string? plcNameFilter)
    {
        var key = CodeIndexCache.BuildKey(projectPath, plcNameFilter);
        return CodeIndexCache.GetOrBuild(key, () => Build(project, plcNameFilter, projectPath));
    }

    private static void WalkBlockGroup(
        PlcBlockGroup group,
        string plcName,
        string? projectPath,
        List<IndexedBlock> blocks,
        ref int skipped,
        ref int total)
    {
        foreach (PlcBlock block in group.Blocks)
        {
            if (blocks.Count >= MaxBlocks)
            {
                return;
            }

            total++;

            if (TryIndex(block, plcName, projectPath, blocks))
            {
                continue;
            }

            skipped++;
        }

        foreach (PlcBlockGroup child in group.Groups)
        {
            WalkBlockGroup(child, plcName, projectPath, blocks, ref skipped, ref total);
        }
    }

    /// <summary>Export a block into the index; if it fails (likely know-how protected),
    /// auto-unlock it once with a cached password and retry. Returns false if it stays unreadable.</summary>
    private static bool TryIndex(PlcBlock block, string plcName, string? projectPath, List<IndexedBlock> blocks)
    {
        if (TryExportAndAdd(block, plcName, blocks))
        {
            return true;
        }

        return KnowHowAutoUnlock.TryUnprotect(block, projectPath)
            && TryExportAndAdd(block, plcName, blocks);
    }

    private static bool TryExportAndAdd(PlcBlock block, string plcName, List<IndexedBlock> blocks)
    {
        try
        {
            var code = ExportBlockText(block);
            blocks.Add(new IndexedBlock
            {
                PlcName = plcName,
                BlockName = block.Name,
                BlockType = BlockTypeName(block),
                ProgrammingLanguage = block.ProgrammingLanguage.ToString(),
                Lines = SplitLines(code),
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string ExportBlockText(PlcBlock block)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tia-mcp-code-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            foreach (var f in Directory.GetFiles(tempDir))
            {
                try { File.Delete(f); } catch { /* ignore */ }
            }

#if LEGACY_TIA
            // V16-V18: legacy Export API (XML text)
            var exportPath = Path.Combine(tempDir, block.Name);
            block.Export(new FileInfo(exportPath), ExportOptions.WithDefaults);
            return BlockSourceReconstructor.Reconstruct(File.ReadAllText(exportPath), block.ProgrammingLanguage.ToString());
#else
            // ExportAsDocuments is the preferred V21 API but throws on some blocks — notably
            // know-how-protected ones whose structure/interface are STILL exportable. Fall
            // back to Export(FileInfo, ExportOptions), the same reliable method the GUI
            // exporter (tia_export_blocks.cs) uses on V21. The legacy XML it produces still
            // carries <Component Name="..."/> symbol references, so search_code / tag_usage
            // can still locate tags used inside these blocks instead of skipping them.
            var exported = TryExportAsDocuments(block, tempDir);
            if (string.IsNullOrEmpty(exported))
            {
                try
                {
                    var path = Path.Combine(tempDir, block.Name + ".xml");
                    block.Export(new FileInfo(path), ExportOptions.WithDefaults);
                    exported = File.ReadAllText(path);
                }
                catch
                {
                    exported = null;
                }
            }

            if (string.IsNullOrEmpty(exported))
            {
                throw new InvalidOperationException("Block export produced no documents (block may be protected or unreadable).");
            }

            // Reconstruct readable source from the tokenized XML so search_code / tag_usage index
            // CODE (e.g. '      =     "TAG"') rather than raw <StlToken>/<Component> XML. STL only;
            // other languages pass through unchanged.
            return BlockSourceReconstructor.Reconstruct(exported, block.ProgrammingLanguage.ToString());
#endif
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }

#if !LEGACY_TIA
    /// <summary>Try the preferred V21 ExportAsDocuments API. Returns the concatenated
    /// document text, or null if it threw / produced nothing (caller falls back).</summary>
    private static string? TryExportAsDocuments(PlcBlock block, string tempDir)
    {
        try
        {
            var result = block.ExportAsDocuments(new DirectoryInfo(tempDir), block.Name);

            var sb = new StringBuilder();
            foreach (FileInfo file in result.ExportedDocuments)
            {
                sb.Append(File.ReadAllText(file.FullName));
                sb.Append('\n');
            }

            return sb.Length == 0 ? null : sb.ToString();
        }
        catch
        {
            return null;
        }
    }
#endif

    private static List<string> SplitLines(string code)
        => new List<string>(code.Replace("\r\n", "\n").Split('\n'));

    internal static string BlockTypeName(PlcBlock block) => block switch
    {
        OB => "OB",
        FB => "FB",
        FC => "FC",
        GlobalDB => "GlobalDB",
        InstanceDB => "InstanceDB",
        ArrayDB => "ArrayDB",
        _ => block.GetType().Name,
    };
}

internal sealed class IndexedBlock
{
    public string PlcName { get; set; } = string.Empty;
    public string BlockName { get; set; } = string.Empty;
    public string BlockType { get; set; } = string.Empty;
    public string ProgrammingLanguage { get; set; } = string.Empty;
    public List<string> Lines { get; set; } = new();
}

internal sealed class IndexBuildResult
{
    public IndexBuildResult(List<IndexedBlock> blocks, int skippedProtected, int totalBlocks)
    {
        Blocks = blocks;
        SkippedProtected = skippedProtected;
        TotalBlocks = totalBlocks;
    }

    public List<IndexedBlock> Blocks { get; }
    public int SkippedProtected { get; }
    public int TotalBlocks { get; }
}
