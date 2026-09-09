using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

/// <summary>
/// Result of a subnet lifecycle request (<c>create_subnet</c>/<c>update_subnet</c>/
/// <c>delete_subnet</c>). Without <c>confirm</c> the payload is a preview (Applied false) —
/// for deletes including the connected nodes that make a subnet refuse deletion without force.
/// </summary>
public class SubnetLifecycleResultInfo
{
    /// <summary>Operation that was requested: "create_subnet", "update_subnet" or "delete_subnet".</summary>
    public string Operation { get; set; } = "";

    /// <summary>Subnet name the request targeted.</summary>
    public string SubnetName { get; set; } = "";

    /// <summary>Network type of the subnet (Ethernet/Profibus; from the request for creates).</summary>
    public string? NetworkType { get; set; }

    /// <summary>New name applied by update_subnet (null = unchanged).</summary>
    public string? NewName { get; set; }

    /// <summary>Openness SubnetId attribute when readable (stable identity beyond the name).</summary>
    public string? SubnetId { get; set; }

    /// <summary>Devices/network interfaces connected to the subnet (delete preview/guard).</summary>
    public List<string> ConnectedNodes { get; } = new();

    /// <summary>False = dry run (confirm was not set, nothing was changed). True = executed.</summary>
    public bool Applied { get; set; }

    public string Message { get; set; } = "";
}
