using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class DeviceItemInfo
{
    public string Name { get; set; } = string.Empty;

    public string TypeIdentifier { get; set; } = string.Empty;

    public int PositionNumber { get; set; } = 0;

    public string? Address { get; set; }

    public List<NetworkInterfaceInfo>? NetworkInterfaces { get; set; }

    public List<DeviceItemInfo>? Items { get; set; }
}
