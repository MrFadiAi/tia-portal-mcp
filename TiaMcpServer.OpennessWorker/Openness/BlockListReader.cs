using System;
using System.Collections.Generic;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Lightweight block listing for one (or all) PLCs: name, type, number, language, path.
/// Token-efficient alternative to browse_project_tree when only a block index is needed.
/// </summary>
public static class BlockListReader
{
    public static List<BlockSummaryInfo> Read(Project project, string? plcNameFilter)
    {
        var result = new List<BlockSummaryInfo>();
        foreach (var (device, plc) in PlcSoftwareFinder.Filter(project, plcNameFilter))
        {
            WalkBlockGroup(plc.BlockGroup, CombinePath(device.Name, "Blocks"), result);
        }

        return result;
    }

    private static void WalkBlockGroup(PlcBlockGroup group, string path, List<BlockSummaryInfo> result)
    {
        foreach (PlcBlock block in group.Blocks)
        {
            try
            {
                result.Add(new BlockSummaryInfo
                {
                    Name = block.Name,
                    BlockType = BlockTypeName(block),
                    Number = block.Number,
                    ProgrammingLanguage = block.ProgrammingLanguage.ToString(),
                    Path = CombinePath(path, block.Name),
                });
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping a block while listing '{group.Name}': {ex.Message}");
            }
        }

        foreach (PlcBlockGroup child in group.Groups)
        {
            WalkBlockGroup(child, CombinePath(path, child.Name), result);
        }
    }

    private static string BlockTypeName(PlcBlock block) => block switch
    {
        OB => "OB",
        FB => "FB",
        FC => "FC",
        GlobalDB => "GlobalDB",
        InstanceDB => "InstanceDB",
        ArrayDB => "ArrayDB",
        _ => block.GetType().Name,
    };

    private static string CombinePath(params string[] segments) => string.Join("/", segments);
}
