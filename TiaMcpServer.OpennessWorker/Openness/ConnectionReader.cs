using Siemens.Engineering;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class ConnectionReader
{
    /// <summary>
    /// Reads connection data by delegating to HardwareConfigReader which already
    /// traverses subnets, IO systems, and device network interfaces correctly
    /// using reflection-based patterns that work across TIA Portal versions.
    /// </summary>
    public static ConnectionReport Read(Project project)
    {
        var hwConfig = HardwareConfigReader.Read(project);

        var report = new ConnectionReport();

        // Map subnets from hardware config
        foreach (var subnet in hwConfig.Subnets)
        {
            report.Subnets.Add(new SubnetInfo
            {
                Name = subnet.Name,
                TypeIdentifier = subnet.TypeIdentifier,
                ConnectedNodeNames = subnet.ConnectedNodeNames,
                IoSystems = subnet.IoSystems
            });
        }

        // Map device network interfaces (walk nested device items)
        foreach (var device in hwConfig.Devices)
        {
            WalkDeviceItems(device.Name, device.Items, report.DeviceInterfaces);
        }

        return report;
    }

    private static void WalkDeviceItems(string deviceName, List<DeviceItemInfo> items, List<DeviceInterfaceInfo> interfaces)
    {
        foreach (var item in items)
        {
            if (item.NetworkInterfaces is not null)
            {
                foreach (var ni in item.NetworkInterfaces)
                {
                    foreach (var node in ni.Nodes)
                    {
                        interfaces.Add(new DeviceInterfaceInfo
                        {
                            DeviceName = deviceName,
                            DeviceItemName = item.Name,
                            NodeType = node.Name,
                            Addresses = new List<AddressInfo>
                            {
                                new() { Address = node.IpAddress ?? "", Type = "IPAddress" },
                                new() { Address = node.SubnetMask ?? "", Type = "SubnetMask" },
                                new() { Address = node.PnDeviceName ?? "", Type = "PROFINET Device Name" }
                            }
                        });
                    }
                }
            }

            // Recurse into child device items
            if (item.Items is not null)
            {
                WalkDeviceItems(deviceName, item.Items, interfaces);
            }
        }
    }
}
