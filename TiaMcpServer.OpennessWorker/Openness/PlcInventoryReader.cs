using System.Collections.Generic;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Lightweight enumeration of every PLC in a project (device + software name + counts).
/// Cheaper than browse_project_tree: no block/tag/type details, just the inventory.
/// </summary>
public static class PlcInventoryReader
{
    public static List<PlcInventoryInfo> ReadAll(Project project)
    {
        var result = new List<PlcInventoryInfo>();
        foreach (var (device, plc) in PlcSoftwareFinder.Enumerate(project))
        {
            result.Add(new PlcInventoryInfo
            {
                DeviceName = device.Name,
                PlcName = plc.Name,
                BlockCount = CountBlocks(plc.BlockGroup),
                TagTableCount = CountTagTables(plc.TagTableGroup),
                TypeCount = CountTypes(plc.TypeGroup),
            });
        }

        return result;
    }

    private static int CountBlocks(PlcBlockGroup group)
    {
        var count = 0;
        try { count += group.Blocks.Count; }
        catch (EngineeringException) { /* ignore */ }

        foreach (PlcBlockGroup child in group.Groups)
        {
            count += CountBlocks(child);
        }

        return count;
    }

    private static int CountTagTables(PlcTagTableGroup group)
    {
        var count = 0;
        try { count += group.TagTables.Count; }
        catch (EngineeringException) { /* ignore */ }

        foreach (PlcTagTableGroup child in group.Groups)
        {
            count += CountTagTables(child);
        }

        return count;
    }

    private static int CountTypes(PlcTypeGroup group)
    {
        var count = 0;
        try { count += group.Types.Count; }
        catch (EngineeringException) { /* ignore */ }

        foreach (PlcTypeGroup child in group.Groups)
        {
            count += CountTypes(child);
        }

        return count;
    }
}
