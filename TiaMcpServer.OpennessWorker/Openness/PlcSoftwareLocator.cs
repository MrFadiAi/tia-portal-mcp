using Siemens.Engineering;
using Siemens.Engineering.SW;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Thin alias kept for its existing callers (tag tables, tag mutations, start_plc/stop_plc).
/// All PLC resolution now goes through <see cref="PlcSoftwareFinder.ResolveUnique"/>, which
/// accepts the device name OR the PLC-software name (case-insensitive) and lists both name
/// forms on a miss — same rule as every inventory reader.
/// </summary>
public static class PlcSoftwareLocator
{
    public static PlcSoftware Find(Project project, string? plcName)
        => PlcSoftwareFinder.ResolveUnique(project, plcName).Plc;
}
