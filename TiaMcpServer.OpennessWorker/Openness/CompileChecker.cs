using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
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
        return PlcSoftwareFinder.Enumerate(project).Select(pair => pair.Device.Name).FirstOrDefault();
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
            if (plcName is null)
            {
                throw new InvalidOperationException("No PLC software was found in the project.");
            }

            // Miss lists both name forms of the available PLCs (same message shape as the
            // other PLC-targeting tools), so the retry uses an accepted name.
            throw new InvalidOperationException(PlcNameMatcher.BuildNotFoundMessage(
                plcName,
                PlcSoftwareFinder.Enumerate(project)
                    .Select(pair => (pair.Device.Name, pair.Plc.Name))));
        }

        foreach (var plc in report.Plcs)
        {
            report.TotalErrorCount += plc.ErrorCount;
            report.TotalWarningCount += plc.WarningCount;
            report.OverallState = WorstState(report.OverallState, plc.State);
        }

        return report;
    }

    /// <summary>
    /// Tolerant PLC resolution (device name OR software name, via the shared
    /// <see cref="PlcSoftwareFinder.Filter"/> rule) — compile_check must accept the same names
    /// every other PLC-targeting tool accepts, including the plcName the UDT-inconsistency
    /// recovery hint prints (which is often the software name, e.g. PLF-00A-PLC_MASTER).
    /// </summary>
    private static IEnumerable<DiscoveredPlcSoftware> FindAllPlcSoftware(Project project, string? plcName)
    {
        foreach (var (device, software) in PlcSoftwareFinder.Filter(project, plcName))
        {
            yield return new DiscoveredPlcSoftware(device.Name, software);
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

    /// <summary>The ONE compile route (compile_check) — also used by the cross-reference
    /// auto-compile so both paths take the same route: FIRST the <see cref="ICompilable"/>
    /// service (the real V16/V18 shape — PlcSoftware declares no Compile method at all),
    /// then the <see cref="CompileMethodFinder"/> method hunt, then the COM late-binding
    /// fallback.</summary>
    internal static CompilerResult CompileObject(object compilable)
    {
        // Route 1 — the compiler as a SERVICE: GetService<ICompilable>() → Compile().
        // Verified against the real V18 PublicAPI DLL: PlcSoftware's declared members are
        // properties + UpdateProgram + GetService[T] only — no Compile method anywhere. The
        // runtime interfaces dump (IEngineeringServiceProvider, ...) confirms the service
        // route is the one Openness provides. V21 wrappers that declare Compile directly
        // resolve the service too, so this route runs first for every version.
        var service = TryResolveCompilableService(compilable);
        if (service is not null)
        {
            return service.Compile();
        }

        var compileMethod = CompileMethodFinder.Find(compilable.GetType());
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
                $"Object '{runtimeType}' exposes neither a Compile method nor an ICompilable service " +
                $"(GetService failed or returned null). Implemented interfaces: [{interfaces}]. " +
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

    /// <summary>Resolve the object's compiler service, tolerating "no service for this
    /// object" (Openness throws an EngineeringException) — null means fall through to the
    /// Compile-method routes.</summary>
    private static ICompilable? TryResolveCompilableService(object compilable)
    {
        try
        {
            return CompileMethodFinder.TryGetService(compilable, typeof(ICompilable)) as ICompilable;
        }
        catch (EngineeringException)
        {
            return null;
        }
    }

    /// <summary>Error-severity compiler messages as flat "path: description" texts (used by
    /// the consistency auto-heal to put the actual errors in front of the user).</summary>
    internal static List<string> ErrorTexts(CompilerResult result)
    {
        var texts = new List<string>();
        foreach (CompilerResultMessage message in result.Messages)
        {
            if (message.ErrorCount <= 0 || string.IsNullOrWhiteSpace(message.Description))
            {
                continue;
            }

            var path = ReadMessagePath(message);
            texts.Add(path.Length > 0 ? $"{path}: {message.Description}" : message.Description);
        }

        return texts;
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
