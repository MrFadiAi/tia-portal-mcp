using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Resolves the single PLC whose tag tables are matched against channels, deterministically and
/// without a first-match fallback: a case-insensitive <c>plcName</c> match against the PLC
/// software name or its owning device name (our PlcSoftwareFinder.Filter semantics), or — when
/// omitted — the only PLC in the project. Notes explain every skipped match (they surface in
/// HardwareConfigInfo.IoNotes); null is returned instead of guessing.
/// </summary>
public static class IoTagIndexResolver
{
    public static IoTagIndex? Resolve(Project project, string? plcName, List<string> notes, out string? sourceDeviceName)
    {
        sourceDeviceName = null;

        var discovered = PlcSoftwareFinder.Filter(project, plcName).ToList();
        var all = PlcSoftwareFinder.Enumerate(project).ToList();

        if (plcName is not null)
        {
            if (discovered.Count == 0)
            {
                notes.Add($"No PLC named '{plcName}' was found; no tag matches are reported.");
                return null;
            }

            if (discovered.Count > 1)
            {
                notes.Add(
                    $"More than one PLC matches '{plcName}' ({string.Join(", ", discovered.Select(d => d.Device.Name))}); " +
                    "no tag matches are reported because the PLC selection is ambiguous.");
                return null;
            }
        }
        else if (all.Count != 1)
        {
            notes.Add(all.Count == 0
                ? "No PLC software was found in the project; no tag matches are reported."
                : "More than one PLC exists and no plcName was supplied; no tag matches are reported. Supply plcName to select one PLC.");
            return null;
        }

        var selected = plcName is not null ? discovered[0] : all[0];
        sourceDeviceName = selected.Device.Name;

        var tables = TagTableReader.ReadAll(project, selected.Device.Name);
        var candidates = tables
            .SelectMany(table => table.Tags.Select(tag => new IoTagCandidate(
                tag.Name,
                tag.DataType,
                tag.LogicalAddress,
                table.Name,
                table.FolderPath)))
            .ToList();

        return new IoTagIndex(selected.Device.Name, candidates);
    }
}
