using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class DeviceInfo
{
    public string Name { get; set; } = string.Empty;

    public string TypeIdentifier { get; set; } = string.Empty;

    public List<DeviceItemInfo> Items { get; set; } = new List<DeviceItemInfo>();
}
