using System;
using System.IO;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Tags;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class TagTableExporter
{
    public static string Export(Project project, string? tableName, string? plcName, string? folderPath)
    {
        var plcSoftware = FindPlcSoftware(project, plcName);

        if (string.IsNullOrWhiteSpace(tableName))
        {
            return ExportAll(plcSoftware.TagTableGroup);
        }

        var tagTable = FindTagTable(plcSoftware.TagTableGroup, tableName!, folderPath)
            ?? throw new InvalidOperationException($"Tag table '{tableName}' not found.");

        return ExportSingle(tagTable);
    }

    private static string ExportSingle(PlcTagTable tagTable)
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "tia-mcp-tags-" + Guid.NewGuid().ToString("N") + ".xml");

        try
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }

            tagTable.Export(new FileInfo(tempFile), ExportOptions.WithDefaults);

            if (!File.Exists(tempFile))
            {
                throw new InvalidOperationException("Tag table export produced no file.");
            }

            return File.ReadAllText(tempFile);
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    private static string ExportAll(PlcTagTableGroup group)
    {
        var results = new System.Text.StringBuilder();

        foreach (PlcTagTable table in group.TagTables)
        {
            try
            {
                results.AppendLine($"=== Tag Table: {table.Name} ===");
                results.AppendLine(ExportSingle(table));
                results.AppendLine();
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping tag table '{table.Name}': {ex.Message}");
            }
        }

        foreach (PlcTagTableGroup childGroup in group.Groups)
        {
            try
            {
                results.AppendLine(ExportAll(childGroup));
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping tag table group '{childGroup.Name}': {ex.Message}");
            }
        }

        return results.ToString();
    }

    private static PlcSoftware FindPlcSoftware(Project project, string? plcName)
    {
        foreach (Device device in project.Devices)
        {
            if (plcName is not null && !string.Equals(device.Name, plcName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (DeviceItem item in device.DeviceItems)
            {
                try
                {
                    var container = item.GetService<SoftwareContainer>();
                    if (container?.Software is PlcSoftware plcSoftware)
                    {
                        return plcSoftware;
                    }
                }
                catch (EngineeringException) { }
            }
        }

        throw plcName is null
            ? new InvalidOperationException("No PLC software found in project.")
            : new InvalidOperationException($"PLC '{plcName}' not found in project.");
    }

    private static PlcTagTable? FindTagTable(PlcTagTableGroup group, string tableName, string? folderPath)
    {
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            var segments = folderPath!.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            PlcTagTableGroup current = group;
            foreach (var segment in segments)
            {
                PlcTagTableGroup? next = null;
                foreach (PlcTagTableGroup childGroup in current.Groups)
                {
                    if (string.Equals(childGroup.Name, segment, StringComparison.OrdinalIgnoreCase))
                    {
                        next = childGroup;
                        break;
                    }
                }

                if (next is null)
                {
                    throw new InvalidOperationException($"Tag table folder '{segment}' not found.");
                }

                current = next;
            }

            return current.TagTables.Find(tableName);
        }

        return FindTagTableRecursive(group, tableName);
    }

    private static PlcTagTable? FindTagTableRecursive(PlcTagTableGroup group, string tableName)
    {
        var found = group.TagTables.Find(tableName);
        if (found is not null)
        {
            return found;
        }

        foreach (PlcTagTableGroup childGroup in group.Groups)
        {
            try
            {
                found = FindTagTableRecursive(childGroup, tableName);
                if (found is not null)
                {
                    return found;
                }
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping tag table group while searching: {ex.Message}");
            }
        }

        return null;
    }
}
