using System;
using System.IO;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Types;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class PlcTypeExporter
{
    public static string Export(Project project, string typeName, string? plcName, string? folderPath)
    {
        var plcType = Locate(project, typeName, plcName, folderPath).Type
            ?? throw new InvalidOperationException($"PLC type '{typeName}' not found.");

        string tempFile = Path.Combine(Path.GetTempPath(), "tia-mcp-type-" + Guid.NewGuid().ToString("N") + ".xml");

        try
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }

            plcType.Export(new FileInfo(tempFile), ExportOptions.WithDefaults);

            if (!File.Exists(tempFile))
            {
                throw new InvalidOperationException("Type export produced no file.");
            }

            return File.ReadAllText(tempFile);
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    private static PlcSoftware FindPlcSoftware(Project project, string? plcName)
        => PlcSoftwareFinder.ResolveUnique(project, plcName).Plc;

    /// <summary>
    /// Locate a PLC type and the group that OWNS it (needed by update_type_content, which
    /// imports into the owning group's Types composition). Returns a null Type (with the
    /// best-effort owning group) when the type does not exist — callers decide whether that
    /// is an error. A bad folderPath still throws (the folder itself must exist).
    /// </summary>
    internal static (PlcType? Type, PlcTypeGroup Group) Locate(
        Project project, string typeName, string? plcName, string? folderPath)
    {
        var plcSoftware = FindPlcSoftware(project, plcName);
        return FindType(plcSoftware.TypeGroup, typeName, folderPath);
    }

    private static (PlcType? Type, PlcTypeGroup Group) FindType(PlcTypeGroup group, string typeName, string? folderPath)
    {
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            var segments = folderPath!.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            PlcTypeGroup current = group;
            foreach (var segment in segments)
            {
                PlcTypeGroup? next = null;
                foreach (PlcTypeGroup childGroup in current.Groups)
                {
                    if (string.Equals(childGroup.Name, segment, StringComparison.OrdinalIgnoreCase))
                    {
                        next = childGroup;
                        break;
                    }
                }

                if (next is null)
                {
                    throw new InvalidOperationException($"Type folder '{segment}' not found.");
                }

                current = next;
            }

            return (current.Types.Find(typeName), current);
        }

        return FindTypeRecursive(group, typeName);
    }

    private static (PlcType? Type, PlcTypeGroup Group) FindTypeRecursive(PlcTypeGroup group, string typeName)
    {
        var found = group.Types.Find(typeName);
        if (found is not null)
        {
            return (found, group);
        }

        foreach (PlcTypeGroup childGroup in group.Groups)
        {
            try
            {
                (found, var owner) = FindTypeRecursive(childGroup, typeName);
                if (found is not null)
                {
                    return (found, owner);
                }
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping type group while searching: {ex.Message}");
            }
        }

        return (null, group);
    }
}
