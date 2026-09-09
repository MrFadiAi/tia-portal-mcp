using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Pure name-matching for PLC resolution (Siemens-free, TDD'd). A tool that takes a
/// <c>plcName</c> must accept EITHER the device name OR the PLC-software name — the same
/// tolerant rule the inventory readers (<see cref="PlcSoftwareFinder.Filter"/>,
/// list_plcs / list_blocks / list_plc_types) already use — so a name copied from one
/// tool's output always works in the next. Also builds the not-found/ambiguous error
/// text, which must list BOTH name forms of every available PLC.
/// </summary>
public static class PlcNameMatcher
{
    /// <summary>Case-insensitive match against the device name OR the PLC-software name.</summary>
    public static bool NameMatches(string? deviceName, string? softwareName, string? requestedName)
        => !string.IsNullOrWhiteSpace(requestedName)
           && (string.Equals(deviceName, requestedName, StringComparison.OrdinalIgnoreCase)
               || string.Equals(softwareName, requestedName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Not-found error listing both name forms of every PLC in the project.</summary>
    public static string BuildNotFoundMessage(
        string requestedName, IEnumerable<(string? DeviceName, string? SoftwareName)> candidates)
        => $"PLC '{requestedName}' not found in project (neither as device name nor as PLC-software name)."
           + DescribeAvailable(candidates);

    /// <summary>Ambiguity error when several PLCs match the requested name.</summary>
    public static string BuildAmbiguousMessage(
        string requestedName, int matchCount, IEnumerable<(string? DeviceName, string? SoftwareName)> candidates)
        => $"PLC name '{requestedName}' is ambiguous ({matchCount} PLCs match by device or software name). "
           + "Use the exact device name or the exact PLC-software name."
           + DescribeAvailable(candidates);

    /// <summary>" Available PLCs (software name / device name): 'X' / 'Y', ..." (both name forms, so
    /// the user can copy either into the next call).</summary>
    public static string DescribeAvailable(IEnumerable<(string? DeviceName, string? SoftwareName)> candidates)
    {
        var listed = candidates
            .Select(c => $"'{c.SoftwareName ?? "?"}' / '{c.DeviceName ?? "?"}'")
            .ToList();

        return listed.Count == 0
            ? " The project has no PLCs."
            : " Available PLCs (software name / device name): " + string.Join(", ", listed) + ".";
    }
}
