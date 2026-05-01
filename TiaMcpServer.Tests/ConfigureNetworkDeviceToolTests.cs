using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;
using TiaMcpServer.Tools;
using TiaMcpServer.Worker;
using Xunit;

namespace TiaMcpServer.Tests;

public class ConfigureNetworkDeviceToolTests
{
    [Fact]
    public void ConfigureNetworkDeviceToolHasMcpMetadata()
    {
        var type = typeof(ConfigureNetworkDeviceTool);

        Assert.NotNull(type.GetCustomAttribute<McpServerToolTypeAttribute>());

        var method = type.GetMethod(
            "ConfigureNetworkDevice",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);

        var toolAttribute = method.GetCustomAttribute<McpServerToolAttribute>();
        Assert.NotNull(toolAttribute);
        Assert.Equal("configure_network_device", toolAttribute.Name);

        var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description;
        Assert.Contains("Configure network identity", description);
        Assert.Contains("Requires confirm=true", description);
    }

    [Fact]
    public async Task ConfigureNetworkDeviceRejectsUnconfirmedRequests()
    {
        var result = await ConfigureNetworkDeviceTool.ConfigureNetworkDevice(
            workerClient: null!,
            deviceName: "PLC_1",
            ipAddress: "192.168.0.10");

        Assert.Contains("Operation not confirmed", result);
        Assert.Contains("confirm=true", result);
    }

    [Fact]
    public void OpennessWorkerClientExposesConfigureNetworkDeviceAsync()
    {
        var method = typeof(OpennessWorkerClient).GetMethod(
            "ConfigureNetworkDeviceAsync",
            new[]
            {
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string)
            });

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string>), method.ReturnType);
    }
}
