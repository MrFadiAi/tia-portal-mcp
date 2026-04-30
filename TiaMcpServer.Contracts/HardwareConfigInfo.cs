using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class HardwareConfigInfo
{
    public List<DeviceInfo> Devices { get; set; } = new List<DeviceInfo>();

    public List<SubnetInfo> Subnets { get; set; } = new List<SubnetInfo>();
}
