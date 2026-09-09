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

    /// <summary>
    /// Structured I/O evidence (addresses + channels) for this device item, present ONLY when the
    /// read_hardware_config request set includeIoDetails/includeTagMatches — a default read omits
    /// it so its payload stays byte-identical to earlier versions.
    /// </summary>
    public DeviceItemIoDetailsInfo? IoDetails { get; set; }
}
