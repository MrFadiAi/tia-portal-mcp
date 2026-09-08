using System;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Pure validation shared by create_block_group/delete_block_group: block-group management
/// needs a DETERMINISTIC path (one that names the PLC, e.g. 'PLC_1/Blocks/Folder[/NewGroup]'),
/// because a bare name is ambiguous across PLCs/units. Siemens-free.
/// </summary>
public static class BlockGroupPathGuard
{
    public static BlockAddress Require(string? groupPath)
    {
        if (string.IsNullOrWhiteSpace(groupPath))
        {
            throw new InvalidOperationException("Group path is required (e.g. 'PLC_1/Blocks/MyGroup').");
        }

        BlockAddress address;
        try
        {
            address = BlockAddress.Parse(groupPath);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(ex.Message);
        }

        if (!address.IsDeterministic)
        {
            throw new InvalidOperationException(
                $"Group path '{groupPath}' must name the PLC explicitly (e.g. 'PLC_1/Blocks/{address.BlockName}'). " +
                "A bare group name is ambiguous when the project has more than one PLC or software unit.");
        }

        return address;
    }

    /// <summary>Display path of the parent (the group path without its last segment).</summary>
    public static string ParentDisplayPath(BlockAddress address)
    {
        var full = address.ToDisplayPath();
        var lastSlash = full.LastIndexOf('/');
        return lastSlash > 0 ? full.Substring(0, lastSlash) : full;
    }
}
