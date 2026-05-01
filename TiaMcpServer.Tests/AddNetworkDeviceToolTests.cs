using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;
using TiaMcpServer.Tools;
using TiaMcpServer.Worker;
using Xunit;

namespace TiaMcpServer.Tests;

public class AddNetworkDeviceToolTests
{
    [Fact]
    public void AddNetworkDeviceToolHasMcpMetadata()
    {
        var type = typeof(AddNetworkDeviceTool);

        Assert.NotNull(type.GetCustomAttribute<McpServerToolTypeAttribute>());

        var method = type.GetMethod(
            "AddNetworkDevice",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);

        var toolAttribute = method.GetCustomAttribute<McpServerToolAttribute>();
        Assert.NotNull(toolAttribute);
        Assert.Equal("add_network_device", toolAttribute.Name);

        var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description;
        Assert.Contains("Insert a device", description);
        Assert.Contains("confirm=true", description);
    }

    [Fact]
    public async Task AddNetworkDeviceRejectsUnconfirmedRequests()
    {
        var result = await AddNetworkDeviceTool.AddNetworkDevice(
            workerClient: null!,
            typeIdentifier: "OrderNumber:6ES7 510-1DJ01-0AB0/V2.0",
            deviceName: "PLC_1");

        Assert.Contains("Operation not confirmed", result);
        Assert.Contains("confirm=true", result);
    }

    [Fact]
    public void OpennessWorkerClientExposesAddNetworkDeviceAsync()
    {
        var method = typeof(OpennessWorkerClient).GetMethod(
            "AddNetworkDeviceAsync",
            new[] { typeof(string), typeof(string), typeof(string), typeof(string) });

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string>), method.ReturnType);
    }
}
