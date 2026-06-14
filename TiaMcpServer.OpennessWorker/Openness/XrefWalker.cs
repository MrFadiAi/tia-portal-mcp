using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Focused views over a compiled <see cref="CrossReferenceReport"/>: a single tag's authoritative
/// read/write locations (tag_xref) and a block's callers/callees (call_graph). Both pierce
/// know-how protection because they read TIA's compiled reference graph, not exported source.
/// </summary>
internal static class XrefWalker
{
    public static CrossReferenceSourceInfo? FindSource(
        IEnumerable<CrossReferenceSourceInfo> sources, string name, string? address = null)
    {
        foreach (var s in sources)
        {
            if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return s;
            }

            if (!string.IsNullOrEmpty(address)
                && string.Equals(s.Address, address, StringComparison.OrdinalIgnoreCase))
            {
                return s;
            }

            if (s.Children is { Count: > 0 })
            {
                var found = FindSource(s.Children, name, address);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    public static IEnumerable<CrossReferenceSourceInfo> AllSources(
        IEnumerable<CrossReferenceSourceInfo> sources)
    {
        foreach (var s in sources)
        {
            yield return s;
            if (s.Children is { Count: > 0 })
            {
                foreach (var c in AllSources(s.Children))
                {
                    yield return c;
                }
            }
        }
    }

    /// <summary>Authoritative read/write locations for one tag, from the compiled cross-reference.</summary>
    public static TagXrefResultInfo BuildTagXref(CrossReferenceReport report, string tag, string? address = null)
    {
        var result = new TagXrefResultInfo { Tag = tag };
        CrossReferenceSourceInfo? match = null;

        foreach (var plc in report.Plcs)
        {
            foreach (var msg in plc.Messages)
            {
                result.Messages.Add(msg);
            }

            if (match is null)
            {
                match = FindSource(plc.Sources, tag.Trim(), address);
                if (match is not null)
                {
                    result.PlcName = plc.PlcName;
                    result.Address = match.Address;
                }
            }
        }

        if (match is null)
        {
            result.Found = false;
            result.Messages.Add($"No compiled cross-reference source matched tag '{tag}'. The project may need compiling, or the tag is unused.");
            return result;
        }

        result.Found = true;
        foreach (var blk in match.References) // blk = a block that uses the tag
        {
            foreach (var loc in blk.Locations)
            {
                result.References.Add(new TagXrefReferenceInfo
                {
                    PlcName = result.PlcName,
                    Block = blk.Name,
                    BlockType = blk.TypeName,
                    Access = loc.Access,
                    ReferenceType = loc.ReferenceType,
                    Location = loc.ReferenceLocation,
                    Address = loc.Address,
                });
            }
        }

        result.ReferenceCount = result.References.Count;
        return result;
    }

    /// <summary>Callers and callees of one block, from the compiled cross-reference.</summary>
    public static CallGraphResultInfo BuildCallGraph(CrossReferenceReport report, string block)
    {
        var result = new CallGraphResultInfo { Block = block };
        CrossReferenceSourceInfo? match = null;

        foreach (var plc in report.Plcs)
        {
            foreach (var msg in plc.Messages)
            {
                result.Messages.Add(msg);
            }

            if (match is null)
            {
                match = FindSource(plc.Sources, block.Trim());
                if (match is not null)
                {
                    result.PlcName = plc.PlcName;
                }
            }
        }

        if (match is null)
        {
            result.Found = false;
            result.Messages.Add($"No compiled cross-reference source matched block '{block}'.");
            return result;
        }

        result.Found = true;

        // Callers = blocks that reference this block.
        foreach (var r in match.References)
        {
            result.Callers.Add(ToNode(r));
        }

        // Callees = sources whose References include this block (what this block references/calls).
        foreach (var s in report.Plcs.SelectMany(p => AllSources(p.Sources)))
        {
            if (string.Equals(s.Name, block, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var r in s.References)
            {
                if (string.Equals(r.Name, block, StringComparison.OrdinalIgnoreCase))
                {
                    result.Callees.Add(ToNode(s.Name, s.TypeName, r));
                }
            }
        }

        return result;
    }

    private static CallGraphNodeInfo ToNode(CrossReferenceTargetInfo r)
        => new()
        {
            Block = r.Name,
            BlockType = r.TypeName,
            ReferenceType = r.Locations.FirstOrDefault()?.ReferenceType ?? string.Empty,
            Location = r.Locations.FirstOrDefault()?.ReferenceLocation ?? string.Empty,
        };

    private static CallGraphNodeInfo ToNode(string name, string type, CrossReferenceTargetInfo r)
        => new()
        {
            Block = name,
            BlockType = type,
            ReferenceType = r.Locations.FirstOrDefault()?.ReferenceType ?? string.Empty,
            Location = r.Locations.FirstOrDefault()?.ReferenceLocation ?? string.Empty,
        };
}
