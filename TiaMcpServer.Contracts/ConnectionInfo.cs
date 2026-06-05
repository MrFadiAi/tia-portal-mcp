using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class ConnectionReport
{
    public List<SubnetInfo> Subnets { get; set; } = new();
    public List<DeviceInterfaceInfo> DeviceInterfaces { get; set; } = new();
    public List<IoSystemInfo> IoSystems { get; set; } = new();
}

public class DeviceInterfaceInfo
{
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceItemName { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public List<AddressInfo> Addresses { get; set; } = new();
}

public class AddressInfo
{
    public string Address { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
