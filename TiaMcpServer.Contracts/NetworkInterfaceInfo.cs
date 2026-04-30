using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class NetworkInterfaceInfo
{
    public string Name { get; set; } = string.Empty;

    public List<NodeInfo> Nodes { get; set; } = new List<NodeInfo>();
}
