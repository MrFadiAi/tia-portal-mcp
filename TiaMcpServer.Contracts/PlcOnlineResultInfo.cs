namespace TiaMcpServer.Contracts;

/// <summary>
/// Result of a <c>start_plc</c>/<c>stop_plc</c> run-state control request. Without
/// <c>confirm</c> the service only reads the current operating state and reports what WOULD
/// happen (<see cref="Applied"/> false); with <c>confirm</c> the transition is executed.
/// </summary>
public class PlcOnlineResultInfo
{
    /// <summary>Operation that was requested: "start_plc" or "stop_plc".</summary>
    public string Operation { get; set; } = "";

    /// <summary>Name of the resolved PLC device/software.</summary>
    public string PlcName { get; set; } = "";

    /// <summary>Operating state read BEFORE the request was processed ("unknown" when the
    /// property is unavailable in this TIA version or the target is not online).</summary>
    public string OperatingStateBefore { get; set; } = "";

    /// <summary>Operating state read AFTER the transition (applied requests only; may lag the
    /// physical CPU by a moment, so it is informational, not a success proof).</summary>
    public string? OperatingStateAfter { get; set; }

    /// <summary>False = dry run (confirm was not set, nothing was changed). True = the
    /// start/stop command was issued to the target.</summary>
    public bool Applied { get; set; }

    public string Message { get; set; } = "";
}
