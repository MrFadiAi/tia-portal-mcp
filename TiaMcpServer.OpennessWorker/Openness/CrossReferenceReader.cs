using System;
using System.Linq;
using System.Reflection;
using Siemens.Engineering;
using Siemens.Engineering.CrossReference;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class CrossReferenceReader
{
    public static CrossReferenceReport Read(Project project, string? plcName, string filterName)
    {
        var filter = ToOpennessFilter(filterName);
        var report = new CrossReferenceReport
        {
            Filter = filterName
        };

        foreach (var plc in FindPlcSoftware(project, plcName))
        {
            report.Plcs.Add(ReadPlc(plc.DeviceName, plc.Software, filter));
        }

        if (report.Plcs.Count == 0)
        {
            var detail = plcName is null ? string.Empty : $" named '{plcName}'";
            throw new InvalidOperationException($"No PLC software{detail} was found in the project.");
        }

        report.TotalSourceCount = report.Plcs.Sum(plc => plc.SourceCount);
        report.TotalReferenceCount = report.Plcs.Sum(plc => plc.ReferenceCount);
        report.TotalLocationCount = report.Plcs.Sum(plc => plc.LocationCount);

        return report;
    }

    private static PlcCrossReferenceInfo ReadPlc(
        string deviceName,
        PlcSoftware plcSoftware,
        CrossReferenceFilter filter)
    {
        var result = new PlcCrossReferenceInfo
        {
            PlcName = deviceName
        };

        CrossReferenceService? service = null;
        try
        {
            service = plcSoftware.GetService<CrossReferenceService>();
        }
        catch (EngineeringException ex)
        {
            result.Messages.Add(
                $"Could not get cross-reference service for PLC '{deviceName}': {ex.Message}. " +
                "The project may need to be compiled first — cross-reference data is generated during compilation.");
            return result;
        }

        if (service is null)
        {
            // Cross-reference service requires compilation. Try to compile and retry.
            result.Messages.Add(
                $"Cross-reference service not available for PLC '{deviceName}'. " +
                "Attempting to compile PLC software to generate cross-reference data...");

            try
            {
                CompilePlcSoftware(plcSoftware);
                result.Messages.Add("Compilation completed. Retrying cross-reference retrieval...");

                service = plcSoftware.GetService<CrossReferenceService>();
            }
            catch (Exception compileEx)
            {
                result.Messages.Add(
                    $"Auto-compile failed: {compileEx.Message}. " +
                    "Cross-reference data requires a compiled project. " +
                    "Compile the PLC software manually and retry.");
                return result;
            }

            if (service is null)
            {
                result.Messages.Add(
                    $"Cross-reference service still unavailable after compilation for PLC '{deviceName}'. " +
                    "The project may be in an inconsistent state. Try closing and reopening the project.");
                return result;
            }
        }

        CrossReferenceResult crossReferenceResult;
        try
        {
            crossReferenceResult = service.GetCrossReferences(filter);
        }
        catch (EngineeringException ex)
        {
            result.Messages.Add(
                $"Could not read cross references for PLC '{deviceName}': {ex.Message}. " +
                "Try compiling the project first and ensure no blocks are currently being edited.");
            return result;
        }

        foreach (SourceObject source in crossReferenceResult.Sources)
        {
            try
            {
                result.Sources.Add(ReadSource(source, result.Messages));
            }
            catch (EngineeringException ex)
            {
                result.Messages.Add($"Skipped a cross-reference source in PLC '{deviceName}': {ex.Message}");
            }
        }

        result.SourceCount = CountSources(result.Sources);
        result.ReferenceCount = CountReferences(result.Sources);
        result.LocationCount = CountLocations(result.Sources);

        return result;
    }

    private static CrossReferenceSourceInfo ReadSource(SourceObject source, List<string> messages)
    {
        var sourceInfo = new CrossReferenceSourceInfo
        {
            Name = SafeString(source.Name),
            TypeName = SafeString(source.TypeName),
            Path = SafeString(source.Path),
            Device = SafeString(source.Device),
            Address = SafeString(source.Address)
        };

        foreach (ReferenceObject reference in source.References)
        {
            try
            {
                sourceInfo.References.Add(ReadReference(reference));
            }
            catch (EngineeringException ex)
            {
                messages.Add($"Skipped a cross-reference target for source '{sourceInfo.Name}': {ex.Message}");
            }
        }

        foreach (SourceObject child in source.Children)
        {
            try
            {
                sourceInfo.Children.Add(ReadSource(child, messages));
            }
            catch (EngineeringException ex)
            {
                messages.Add($"Skipped a child cross-reference source for source '{sourceInfo.Name}': {ex.Message}");
            }
        }

        return sourceInfo;
    }

    private static CrossReferenceTargetInfo ReadReference(ReferenceObject reference)
    {
        var referenceInfo = new CrossReferenceTargetInfo
        {
            Name = SafeString(reference.Name),
            TypeName = SafeString(reference.TypeName),
            Path = SafeString(reference.Path),
            Device = SafeString(reference.Device),
            Address = SafeString(reference.Address)
        };

        foreach (Location location in reference.Locations)
        {
            referenceInfo.Locations.Add(ReadLocation(location));
        }

        return referenceInfo;
    }

    private static CrossReferenceLocationInfo ReadLocation(Location location)
    {
        return new CrossReferenceLocationInfo
        {
            Name = SafeString(location.Name),
            TypeName = SafeString(location.TypeName),
            Address = SafeString(location.Address),
            Access = location.Access.ToString(),
            ReferenceType = location.ReferenceType.ToString(),
            ReferenceLocation = SafeString(location.ReferenceLocation),
            ReferencedAs = SafeString(location.ReferencedAs),
            ReferencedAsName = SafeString(location.ReferencedAsName)
        };
    }

    private static IEnumerable<DiscoveredPlcSoftware> FindPlcSoftware(Project project, string? plcName)
    {
        foreach (Device device in project.Devices)
        {
            if (plcName is not null &&
                !string.Equals(device.Name, plcName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (PlcSoftware plcSoftware in FindPlcSoftwareInDeviceItems(device.DeviceItems))
            {
                yield return new DiscoveredPlcSoftware(device.Name, plcSoftware);
            }
        }
    }

    private static IEnumerable<PlcSoftware> FindPlcSoftwareInDeviceItems(DeviceItemComposition items)
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

            foreach (var child in FindPlcSoftwareInDeviceItems(item.DeviceItems))
            {
                yield return child;
            }
        }
    }

    private static CrossReferenceFilter ToOpennessFilter(string filterName)
    {
        return filterName switch
        {
            CrossReferenceFilterNames.AllObjects => CrossReferenceFilter.AllObjects,
            CrossReferenceFilterNames.ObjectsWithReferences => CrossReferenceFilter.ObjectsWithReferences,
            CrossReferenceFilterNames.ObjectsWithoutReferences => CrossReferenceFilter.ObjectsWithoutReferences,
            CrossReferenceFilterNames.UnusedObjects => CrossReferenceFilter.UnusedObjects,
            _ => throw new InvalidOperationException($"Unsupported normalized cross-reference filter '{filterName}'.")
        };
    }

    private static int CountSources(IEnumerable<CrossReferenceSourceInfo> sources)
    {
        return sources.Sum(source => 1 + CountSources(source.Children));
    }

    private static int CountReferences(IEnumerable<CrossReferenceSourceInfo> sources)
    {
        return sources.Sum(source => source.References.Count + CountReferences(source.Children));
    }

    private static int CountLocations(IEnumerable<CrossReferenceSourceInfo> sources)
    {
        return sources.Sum(source =>
            source.References.Sum(reference => reference.Locations.Count) + CountLocations(source.Children));
    }

    private static string SafeString(object? value)
    {
        return value?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Compile PLC software via reflection (same approach as CompileChecker).
    /// Cross-reference service requires compiled data to be available.
    /// </summary>
    private static void CompilePlcSoftware(PlcSoftware plcSoftware)
    {
        var type = plcSoftware.GetType();
        var compileMethod = type.GetMethod("Compile", BindingFlags.Instance | BindingFlags.Public)
            ?? type.GetMethod("Compile", BindingFlags.Instance | BindingFlags.NonPublic);

        if (compileMethod is null)
        {
            // Search interfaces (explicit implementations are private)
            foreach (var iface in type.GetInterfaces())
            {
                compileMethod = iface.GetMethod("Compile", BindingFlags.Instance | BindingFlags.Public);
                if (compileMethod is not null) break;
            }
        }

        if (compileMethod is null)
        {
            throw new InvalidOperationException("PlcSoftware does not expose a Compile method.");
        }

        compileMethod.Invoke(plcSoftware, null);
    }

    private sealed class DiscoveredPlcSoftware
    {
        public DiscoveredPlcSoftware(string deviceName, PlcSoftware software)
        {
            DeviceName = deviceName;
            Software = software;
        }

        public string DeviceName { get; }

        public PlcSoftware Software { get; }
    }
}
