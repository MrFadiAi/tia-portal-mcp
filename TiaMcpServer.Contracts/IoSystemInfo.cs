using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class IoSystemInfo
{
    public string Name { get; set; } = string.Empty;

    public string? IoControllerName { get; set; }

    public List<string> ConnectedDeviceNames { get; set; } = new List<string>();
}
