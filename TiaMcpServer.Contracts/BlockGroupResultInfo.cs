namespace TiaMcpServer.Contracts;

/// <summary>
/// Result of a <c>create_block_group</c>/<c>delete_block_group</c> request. Without
/// <c>confirm</c> the payload is a preview (<see cref="Applied"/> false) that reports what
/// would happen — for deletes including the blast radius (block/subgroup counts).
/// </summary>
public class BlockGroupResultInfo
{
    /// <summary>Operation that was requested: "create_block_group" or "delete_block_group".</summary>
    public string Operation { get; set; } = "";

    /// <summary>Full display path of the group the request targets (ends with the group name).</summary>
    public string GroupPath { get; set; } = "";

    /// <summary>Name of the group itself.</summary>
    public string GroupName { get; set; } = "";

    /// <summary>Display path of the parent group the group lives in.</summary>
    public string ParentPath { get; set; } = "";

    /// <summary>Blocks directly inside the group (delete preview/apply).</summary>
    public int? DirectBlocks { get; set; }

    /// <summary>Direct child groups (delete preview/apply).</summary>
    public int? Subgroups { get; set; }

    /// <summary>Blocks in the group AND all nested groups (delete blast radius).</summary>
    public int? TotalBlocks { get; set; }

    /// <summary>False = dry run (confirm was not set, nothing was changed). True = executed.</summary>
    public bool Applied { get; set; }

    public string Message { get; set; } = "";
}
