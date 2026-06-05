using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class BlockExporter
{
    public static string Export(Project project, string blockPath)
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
            return File.ReadAllText(exportPath);
#else
            DocumentExportResult result = target.Block!.ExportAsDocuments(new DirectoryInfo(tempDir), target.DocumentName);

            if (result.State != DocumentResultState.Success)
                throw new InvalidOperationException($"Export failed with state: {result.State}");

            var combined = new StringBuilder();
            foreach (FileInfo file in result.ExportedDocuments)
            {
                combined.Append($"--- FILE: {file.Name} ---\n");
                combined.Append(File.ReadAllText(file.FullName));
            }

            return combined.ToString();
#endif
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
