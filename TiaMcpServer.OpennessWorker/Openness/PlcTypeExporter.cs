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
        var plcSoftware = FindPlcSoftware(project, plcName);
        var plcType = FindType(plcSoftware.TypeGroup, typeName, folderPath)
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

    private static PlcType? FindType(PlcTypeGroup group, string typeName, string? folderPath)
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

            return current.Types.Find(typeName);
        }

        return FindTypeRecursive(group, typeName);
    }

    private static PlcType? FindTypeRecursive(PlcTypeGroup group, string typeName)
    {
        var found = group.Types.Find(typeName);
        if (found is not null)
        {
            return found;
        }

        foreach (PlcTypeGroup childGroup in group.Groups)
        {
            try
            {
                found = FindTypeRecursive(childGroup, typeName);
                if (found is not null)
                {
                    return found;
                }
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping type group while searching: {ex.Message}");
            }
        }

        return null;
    }
}
