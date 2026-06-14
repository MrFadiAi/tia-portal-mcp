using System;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Deletes a PLC block by path. Captures metadata before deletion (the block
/// object is invalid after Delete()).
/// </summary>
public static class BlockDeleter
{
    public static BlockDeletionResultInfo Delete(Project project, string blockPath)
    {
        var address = BlockAddress.Parse(blockPath);
        var target = BlockTargetResolver.ResolveForExport(project, address);
        var block = target.Block
            ?? throw new InvalidOperationException($"Block '{blockPath}' not found.");

        // Capture identity before the object is deleted.
        var name = block.Name;
        var number = block.Number;
        var typeName = BlockTypeName(block);

        block.Delete();

        return new BlockDeletionResultInfo
        {
            BlockPath = blockPath,
            BlockName = name,
            BlockNumber = number,
            BlockType = typeName,
            Success = true,
        };
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
}
