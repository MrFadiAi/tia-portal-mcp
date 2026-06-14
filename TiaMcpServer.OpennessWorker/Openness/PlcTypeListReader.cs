using System;
using System.Collections.Generic;
using Siemens.Engineering;
using Siemens.Engineering.SW.Types;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Lightweight listing of PLC user types (UDTs) for one or all PLCs: name + path.
/// </summary>
public static class PlcTypeListReader
{
    public static List<PlcTypeInfo> Read(Project project, string? plcNameFilter)
    {
        var result = new List<PlcTypeInfo>();
        foreach (var (_, plc) in PlcSoftwareFinder.Filter(project, plcNameFilter))
        {
            WalkTypeGroup(plc.TypeGroup, CombinePath(plc.Name, "Types"), result);
        }

        return result;
    }

    private static void WalkTypeGroup(PlcTypeGroup group, string path, List<PlcTypeInfo> result)
    {
        foreach (PlcType type in group.Types)
        {
            try
            {
                result.Add(new PlcTypeInfo
                {
                    Name = type.Name,
                    Path = CombinePath(path, type.Name),
                });
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping a type while listing '{group.Name}': {ex.Message}");
            }
        }

        foreach (PlcTypeGroup child in group.Groups)
        {
            WalkTypeGroup(child, CombinePath(path, child.Name), result);
        }
    }

    private static string CombinePath(params string[] segments) => string.Join("/", segments);
}
