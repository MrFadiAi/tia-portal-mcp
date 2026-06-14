using System;
using System.Collections.Generic;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Shared helper that enumerates PLC software containers in a TIA Portal project.
/// Centralised so list_plcs / list_blocks / browse_project_tree all agree on what
/// counts as a "PLC" and accept either the device name or the PLC-software name.
/// </summary>
internal static class PlcSoftwareFinder
{
    /// <summary>Enumerate every (device, PlcSoftware) pair found anywhere in the project.</summary>
    public static IEnumerable<(Device Device, PlcSoftware Plc)> Enumerate(Project project)
    {
        foreach (Device device in project.Devices)
        {
            foreach (var plc in FindInDevice(device))
            {
                yield return (device, plc);
            }
        }
    }

    /// <summary>
    /// Enumerate PLCs whose device name OR PLC-software name matches the filter
    /// (case-insensitive). If filter is null/empty, all PLCs are returned.
    /// </summary>
    public static IEnumerable<(Device Device, PlcSoftware Plc)> Filter(
        Project project, string? plcNameFilter)
    {
        var filter = string.IsNullOrWhiteSpace(plcNameFilter) ? null : plcNameFilter!.Trim();
        foreach (var (device, plc) in Enumerate(project))
        {
            if (filter is null
                || string.Equals(device.Name, filter, StringComparison.OrdinalIgnoreCase)
                || string.Equals(plc.Name, filter, StringComparison.OrdinalIgnoreCase))
            {
                yield return (device, plc);
            }
        }
    }

    public static IEnumerable<PlcSoftware> FindInDevice(Device device)
        => FindInDeviceItems(device.DeviceItems);

    private static IEnumerable<PlcSoftware> FindInDeviceItems(DeviceItemComposition items)
    {
        foreach (DeviceItem item in items)
        {
            PlcSoftware? plcSoftware = null;
            try
            {
                var container = item.GetService<SoftwareContainer>();
                plcSoftware = container?.Software as PlcSoftware;
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping a device item while locating PLC software: {ex.Message}");
            }

            if (plcSoftware is not null)
            {
                yield return plcSoftware;
            }

            foreach (var child in FindInDeviceItems(item.DeviceItems))
            {
                yield return child;
            }
        }
    }
}
