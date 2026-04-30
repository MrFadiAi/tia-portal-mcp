using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class NodeInfo
{
    public string Name { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public string? SubnetMask { get; set; }

    public string? PnDeviceName { get; set; }

    public string? SubnetName { get; set; }

    public string? IoSystemName { get; set; }
}
