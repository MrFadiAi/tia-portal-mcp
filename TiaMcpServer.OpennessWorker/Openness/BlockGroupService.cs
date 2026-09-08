using System;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Block-folder lifecycle (<c>create_block_group</c>/<c>delete_block_group</c>). Mirrors the
/// upstream BlockMutationService group route: resolve the parent group by deterministic path,
/// then <c>parentGroup.Groups.Create(name)</c> / user-group <c>Delete()</c>. Without confirm
/// every call is a preview that reports what would happen (deletes include the blast radius:
/// direct/total block and subgroup counts).
/// </summary>
public static class BlockGroupService
{
    public const string CreateOperation = "create_block_group";
    public const string DeleteOperation = "delete_block_group";

    public static BlockGroupResultInfo Create(Project project, string groupPath, bool confirm)
    {
        var address = BlockGroupPathGuard.Require(groupPath);
        PlcBlockGroup parent = BlockTargetResolver.ResolveParentGroup(project, address);

        if (TryFindChildGroup(parent, address.BlockName) is not null)
        {
            throw new InvalidOperationException(
                $"A block group named '{address.BlockName}' already exists under '{BlockGroupPathGuard.ParentDisplayPath(address)}'.");
        }

        if (!confirm)
        {
            return Preview(CreateOperation, address,
                $"DRY RUN — nothing was changed. Call again with confirm=true to create block group " +
                $"'{address.ToDisplayPath()}'.");
        }

        parent.Groups.Create(address.BlockName);

        return Applied(CreateOperation, address,
            $"Block group '{address.ToDisplayPath()}' was created.");
    }

    public static BlockGroupResultInfo Delete(Project project, string groupPath, bool confirm)
    {
        var address = BlockGroupPathGuard.Require(groupPath);
        PlcBlockGroup parent = BlockTargetResolver.ResolveParentGroup(project, address);

        PlcBlockGroup? group = TryFindChildGroup(parent, address.BlockName)
            ?? throw new InvalidOperationException($"Block group '{address.ToDisplayPath()}' was not found.");

        if (group is not PlcBlockUserGroup userGroup)
        {
            throw new InvalidOperationException(
                $"Block group '{address.ToDisplayPath()}' is a system group and cannot be deleted. " +
                "Only user-created block groups can be deleted.");
        }

        int directBlocks = SafeCount(group.Blocks);
        int subgroups = SafeCount(group.Groups);
        int totalBlocks = CountBlocksRecursive(group);

        if (!confirm)
        {
            var blastRadius =
                $"DRY RUN — nothing was changed. Deleting '{address.ToDisplayPath()}' removes the group and " +
                $"EVERYTHING inside it: {totalBlocks} block(s) ({directBlocks} direct, {totalBlocks - directBlocks} in {subgroups} nested group(s)). " +
                $"Call again with confirm=true to delete.";
            return Preview(DeleteOperation, address, blastRadius, directBlocks, subgroups, totalBlocks);
        }

        userGroup.Delete();

        return Applied(DeleteOperation, address,
            $"Block group '{address.ToDisplayPath()}' was deleted ({totalBlocks} block(s), {subgroups} nested group(s) removed).",
            directBlocks, subgroups, totalBlocks);
    }

    private static PlcBlockGroup? TryFindChildGroup(PlcBlockGroup parent, string name)
    {
        foreach (PlcBlockGroup child in parent.Groups)
        {
            if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// Count any Openness composition defensively (foreach, not ICollection.Count — the
    /// compile-time stub compositions only guarantee enumeration). A protected group can
    /// throw mid-enumeration; count what is readable.
    /// </summary>
    private static int SafeCount(System.Collections.IEnumerable sequence)
    {
        try
        {
            int count = 0;
            if (sequence is not null)
            {
                foreach (var _ in sequence)
                {
                    count++;
                }
            }

            return count;
        }
        catch (EngineeringException)
        {
            return 0;
        }
    }

    private static int CountBlocksRecursive(PlcBlockGroup group)
    {
        int total = SafeCount(group.Blocks);
        foreach (PlcBlockGroup child in group.Groups)
        {
            total += CountBlocksRecursive(child);
        }

        return total;
    }

    private static BlockGroupResultInfo Preview(
        string operation, BlockAddress address, string message,
        int? directBlocks = null, int? subgroups = null, int? totalBlocks = null)
        => Build(operation, address, applied: false, message, directBlocks, subgroups, totalBlocks);

    private static BlockGroupResultInfo Applied(
        string operation, BlockAddress address, string message,
        int? directBlocks = null, int? subgroups = null, int? totalBlocks = null)
        => Build(operation, address, applied: true, message, directBlocks, subgroups, totalBlocks);

    private static BlockGroupResultInfo Build(
        string operation, BlockAddress address, bool applied, string message,
        int? directBlocks, int? subgroups, int? totalBlocks)
        => new()
        {
            Operation = operation,
            GroupPath = address.ToDisplayPath(),
            GroupName = address.BlockName,
            ParentPath = BlockGroupPathGuard.ParentDisplayPath(address),
            DirectBlocks = directBlocks,
            Subgroups = subgroups,
            TotalBlocks = totalBlocks,
            Applied = applied,
            Message = message
        };
}
