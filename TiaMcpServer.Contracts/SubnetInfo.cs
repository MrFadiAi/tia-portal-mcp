using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class SubnetInfo
{
    public string Name { get; set; } = string.Empty;

    public string? TypeIdentifier { get; set; }

    public List<IoSystemInfo> IoSystems { get; set; } = new List<IoSystemInfo>();

    public List<string> ConnectedNodeNames { get; set; } = new List<string>();
}
