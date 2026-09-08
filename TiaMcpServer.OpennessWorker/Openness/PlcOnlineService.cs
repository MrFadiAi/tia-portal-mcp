using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Siemens.Engineering;
using Siemens.Engineering.Online;
using Siemens.Engineering.SW;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// PLC run-state control (<c>start_plc</c>/<c>stop_plc</c>). Mirrors the upstream
/// OnlineProvider route: <see cref="PlcSoftware"/>.GetService&lt;<see cref="OnlineProvider"/>&gt;()
/// plus reflection-invoked Start()/Stop() (not declared on the compile-time Openness stub —
/// resolved at runtime from the full assembly, same pattern as CompileChecker.ReadMessagePath).
/// The operating state is read (reflectively) before every request; without confirm the call is
/// a dry run that reports the current state and what would happen.
/// </summary>
public static class PlcOnlineService
{
    public const string StartOperation = "start_plc";
    public const string StopOperation = "stop_plc";

    public static PlcOnlineResultInfo Start(Project project, string? plcName, bool confirm)
        => Control(project, plcName, "Start", StartOperation, "start the PLC", confirm);

    public static PlcOnlineResultInfo Stop(Project project, string? plcName, bool confirm)
        => Control(project, plcName, "Stop", StopOperation, "stop the PLC", confirm);

    private static PlcOnlineResultInfo Control(
        Project project,
        string? plcName,
        string methodName,
        string operation,
        string actionDescription,
        bool confirm)
    {
        PlcSoftware plcSoftware = PlcSoftwareLocator.Find(project, plcName);

        string stateBefore = ReadOperatingState(plcSoftware);

        if (!confirm)
        {
            return new PlcOnlineResultInfo
            {
                Operation = operation,
                PlcName = plcSoftware.Name,
                OperatingStateBefore = stateBefore,
                Applied = false,
                Message =
                    $"DRY RUN — nothing was changed. Current operating state: {stateBefore}. " +
                    $"Call again with confirm=true to {actionDescription} '{plcSoftware.Name}'. " +
                    "The PLC must be reachable (online) for the transition to succeed."
            };
        }

        var onlineProvider = plcSoftware.GetService<OnlineProvider>()
            ?? throw new InvalidOperationException(
                $"OnlineProvider service is not available on PLC '{plcSoftware.Name}'. " +
                "Ensure the PLC is reachable and configured for online access before starting/stopping it.");

        InvokeControlMethod(onlineProvider, methodName, plcSoftware.Name);

        string stateAfter = ReadOperatingState(plcSoftware);
        return new PlcOnlineResultInfo
        {
            Operation = operation,
            PlcName = plcSoftware.Name,
            OperatingStateBefore = stateBefore,
            OperatingStateAfter = stateAfter,
            Applied = true,
            Message =
                $"{operation} command was issued to '{plcSoftware.Name}' " +
                $"(state before: {stateBefore}, state after read-back: {stateAfter}). " +
                "The read-back may lag the physical CPU by a moment."
        };
    }

    /// <summary>
    /// PlcSoftware.OperatingState is not declared on the compile-time stub, so it is read via
    /// reflection. Returns "unknown" (never throws) when the property is missing or unreadable.
    /// </summary>
    private static string ReadOperatingState(PlcSoftware plcSoftware)
    {
        try
        {
            var property = plcSoftware.GetType().GetProperty("OperatingState");
            var value = property?.GetValue(plcSoftware, null);
            return value is null ? "unknown" : value.ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private static void InvokeControlMethod(OnlineProvider onlineProvider, string methodName, string plcName)
    {
        var method = onlineProvider.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);

        if (method is null)
        {
            throw new InvalidOperationException(
                $"OnlineProvider.{methodName}() is not available in this TIA Portal version. " +
                $"PLC: '{plcName}'. Verify the PLC is online before calling this operation.");
        }

        try
        {
            method.Invoke(onlineProvider, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
