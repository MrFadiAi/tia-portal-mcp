using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class CompileChecker
{
    public static CompileCheckReport Compile(Project project, string? plcName, string? blockPath)
    {
        if (!string.IsNullOrWhiteSpace(blockPath))
        {
            return CompileBlock(project, plcName, blockPath!);
        }

        return CompilePlcSoftware(project, plcName);
    }

    private static CompileCheckReport CompileBlock(Project project, string? plcName, string blockPath)
    {
        var address = BlockAddress.Parse(blockPath);
        if (address.PlcName == null && !string.IsNullOrWhiteSpace(plcName))
        {
            address = BlockAddress.Parse(plcName + "/" + blockPath);
        }

        var target = BlockTargetResolver.ResolveForExport(project, address);

        if (target.Block == null)
        {
            throw new InvalidOperationException($"Block '{address.BlockName}' not found.");
        }

        var result = CompileObject(target.Block);
        string resolvedPlcName = address.PlcName ?? string.Empty;
        var usedFirstPlc = false;
        if (string.IsNullOrEmpty(resolvedPlcName))
        {
            resolvedPlcName = FindFirstDeviceName(project) ?? string.Empty;
            usedFirstPlc = true;
        }

        var plc = BuildPlcCompileInfo(resolvedPlcName, result);
        if (usedFirstPlc)
        {
            plc.DiagnosticNotes.Add("No PLC qualifier was specified; compiled using the first PLC found.");
        }

        var report = new CompileCheckReport
        {
            Scope = "block",
            BlockPath = blockPath,
            TotalErrorCount = plc.ErrorCount,
            TotalWarningCount = plc.WarningCount,
            OverallState = plc.State
        };

        report.Plcs.Add(plc);
        return report;
    }

    private static string? FindFirstDeviceName(Project project)
    {
        return FindAllPlcSoftware(project, null).FirstOrDefault()?.DeviceName;
    }

    private static CompileCheckReport CompilePlcSoftware(Project project, string? plcName)
    {
        var report = new CompileCheckReport
        {
            Scope = "plc",
            OverallState = "Success"
        };

        foreach (var plc in FindAllPlcSoftware(project, plcName))
        {
            try
            {
                var result = CompileObject(plc.Software);
                report.Plcs.Add(BuildPlcCompileInfo(plc.DeviceName, result));
            }
            catch (EngineeringException ex)
            {
                var failed = new PlcCompileInfo
                {
                    PlcName = plc.DeviceName,
                    State = "Error"
                };
                failed.DiagnosticNotes.Add($"Compile failed for PLC '{plc.DeviceName}': {ex.Message}");
                report.Plcs.Add(failed);
            }
        }

        if (report.Plcs.Count == 0)
        {
            var detail = plcName is null ? string.Empty : $" named '{plcName}'";
            throw new InvalidOperationException($"No PLC software{detail} was found in the project.");
        }

        foreach (var plc in report.Plcs)
        {
            report.TotalErrorCount += plc.ErrorCount;
            report.TotalWarningCount += plc.WarningCount;
            report.OverallState = WorstState(report.OverallState, plc.State);
        }

        return report;
    }

    private static IEnumerable<DiscoveredPlcSoftware> FindAllPlcSoftware(Project project, string? plcName)
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

    private static PlcCompileInfo BuildPlcCompileInfo(string plcName, CompilerResult result)
    {
        return new PlcCompileInfo
        {
            PlcName = plcName,
            State = MapState(result.State),
            ErrorCount = result.ErrorCount,
            WarningCount = result.WarningCount,
            Messages = MapMessages(result.Messages)
        };
    }

    private static CompilerResult CompileObject(object compilable)
    {
        var compileMethod = FindCompileMethod(compilable.GetType());
        if (compileMethod == null)
        {
            // Fallback: try COM late-binding via Type.InvokeMember (V21 COM interop wrappers)
            try
            {
                var result = compilable.GetType().InvokeMember(
                    "Compile",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod,
                    null,
                    compilable,
                    null);
                if (result is CompilerResult cr)
                    return cr;
            }
            catch (MissingMethodException)
            {
                // Not available via COM either
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }

            var runtimeType = compilable.GetType().FullName ?? compilable.GetType().Name;
            var interfaces = string.Join(", ", compilable.GetType().GetInterfaces().Select(i => i.Name));
            throw new InvalidOperationException(
                $"Object '{runtimeType}' does not expose a Compile method. " +
                $"Implemented interfaces: [{interfaces}]. " +
                "The PLC software may not support compilation through the Openness API in this state.");
        }

        try
        {
            return (CompilerResult)compileMethod.Invoke(compilable, null)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static MethodInfo? FindCompileMethod(Type type)
    {
        const BindingFlags allFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // 1. Search the type itself (public and non-public — explicit interface implementations are private)
        var compileMethod = type.GetMethod("Compile", allFlags);
        if (compileMethod != null)
        {
            return compileMethod;
        }

        // 2. Search all implemented interfaces
        foreach (var interfaceType in type.GetInterfaces())
        {
            compileMethod = interfaceType.GetMethod("Compile", BindingFlags.Instance | BindingFlags.Public);
            if (compileMethod != null)
            {
                return compileMethod;
            }
        }

        // 3. Walk base types (COM wrappers may hide Compile in a base class)
        var baseType = type.BaseType;
        while (baseType is not null)
        {
            compileMethod = baseType.GetMethod("Compile", allFlags);
            if (compileMethod != null)
            {
                return compileMethod;
            }
            baseType = baseType.BaseType;
        }

        return null;
    }

    private static string MapState(CompilerResultState state)
    {
        switch (state)
        {
            case CompilerResultState.Success:
                return "Success";
            case CompilerResultState.Warning:
                return "Warning";
            case CompilerResultState.Error:
                return "Error";
            default:
                return state.ToString();
        }
    }

    private static List<CompileMessageInfo> MapMessages(IEnumerable<CompilerResultMessage> messages)
    {
        var result = new List<CompileMessageInfo>();

        foreach (CompilerResultMessage message in messages)
        {
            result.Add(new CompileMessageInfo
            {
                Description = message.Description,
                Path = ReadMessagePath(message),
                Severity = MapMessageSeverity(message)
            });
        }

        return result;
    }

    private static string MapMessageSeverity(CompilerResultMessage message)
    {
        if (message.ErrorCount > 0)
        {
            return "Error";
        }

        if (message.WarningCount > 0)
        {
            return "Warning";
        }

        return "Information";
    }

    private static string ReadMessagePath(CompilerResultMessage message)
    {
        // Path is not declared on the compile-time Openness stub; resolved at runtime from the full V21 assembly.
        PropertyInfo? property = message.GetType().GetProperty("Path");
        return property?.GetValue(message, null)?.ToString() ?? string.Empty;
    }

    private static string WorstState(string current, string candidate)
    {
        if (current == "Error" || candidate == "Error")
        {
            return "Error";
        }

        if (current == "Warning" || candidate == "Warning")
        {
            return "Warning";
        }

        return "Success";
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
