using System.Text.Json;
using TiaMcpServer.Worker;
using Xunit;

namespace TiaMcpServer.Tests;

public class ScanProjectTreeParserTests
{
    private const string CopiedDeviceTree = """
        {
          "name": "Project", "nodeType": "Project",
          "children": [
            {
              "name": "PLF-01A-PLC_9", "nodeType": "Device",
              "children": [
                {
                  "name": "PLF-00A-PLC_MASTER", "nodeType": "PlcSoftware",
                  "children": [
                    { "name": "Main", "nodeType": "OB" },
                    { "name": "MotorCtrl", "nodeType": "FB" },
                    { "name": "IO tags", "nodeType": "TagTable" }
                  ]
                }
              ]
            },
            {
              "name": "HMI_PANEL_1", "nodeType": "Device",
              "children": [
                { "name": "HMI_PANEL_1", "nodeType": "HmiSoftware", "children": [] }
              ]
            },
            {
              "name": "PLF-02B-PLC_X", "nodeType": "Device",
              "children": [
                {
                  "name": "PLF-02B-PLC_X", "nodeType": "PlcSoftware",
                  "children": [
                    { "name": "Main", "nodeType": "OB" }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void PairsPlcSoftwareWithOwningDevice()
    {
        var s = ScanProjectTreeParser.Parse(CopiedDeviceTree, 21, "V21");

        Assert.Equal(2, s.PlcDevices.Count);
        Assert.Contains(s.PlcDevices, p => p.DeviceName == "PLF-01A-PLC_9" && p.SoftwareName == "PLF-00A-PLC_MASTER");
        Assert.Contains(s.PlcDevices, p => p.DeviceName == "PLF-02B-PLC_X" && p.SoftwareName == "PLF-02B-PLC_X");
    }

    [Fact]
    public void HmiDevicesContributeNoPlcPair()
    {
        var s = ScanProjectTreeParser.Parse(CopiedDeviceTree, 21, "V21");

        Assert.DoesNotContain(s.PlcDevices, p => p.DeviceName == "HMI_PANEL_1" || p.SoftwareName == "HMI_PANEL_1");
    }

    [Fact]
    public void FlatListsAndCountsUnchanged()
    {
        var s = ScanProjectTreeParser.Parse(CopiedDeviceTree, 21, "V21");

        Assert.Equal(new[] { "PLF-00A-PLC_MASTER", "PLF-02B-PLC_X" }, s.PlcNames);
        Assert.Equal(new[] { "PLF-01A-PLC_9", "HMI_PANEL_1", "PLF-02B-PLC_X" }, s.DeviceNames);
        Assert.Equal(3, s.DeviceCount);
        Assert.Equal(3, s.BlockCount); // Main(OB) + MotorCtrl(FB) + Main(OB)
        Assert.Equal(1, s.TagTableCount);
        Assert.Equal("PLF-01A-PLC_9, HMI_PANEL_1, PLF-02B-PLC_X", s.Name);
    }

    [Fact]
    public void PlcSoftwareOutsideAnyDeviceSelfPairs()
    {
        const string tree = """
            { "name": "root", "nodeType": "Project", "children": [
                { "name": "LonePlc", "nodeType": "PlcSoftware", "children": [] }
            ] }
            """;

        var s = ScanProjectTreeParser.Parse(tree, 18, "V18");

        var pair = Assert.Single(s.PlcDevices);
        Assert.Equal("LonePlc", pair.DeviceName);
        Assert.Equal("LonePlc", pair.SoftwareName);
    }

    [Fact]
    public void DuplicatePairsAreDedupedCaseInsensitively()
    {
        const string tree = """
            { "name": "root", "nodeType": "Project", "children": [
                { "name": "DEV_1", "nodeType": "Device", "children": [
                    { "name": "SW_1", "nodeType": "PlcSoftware", "children": [] },
                    { "name": "sw_1", "nodeType": "PlcSoftware", "children": [] }
                ] }
            ] }
            """;

        var s = ScanProjectTreeParser.Parse(tree, 16, "V16");

        var pair = Assert.Single(s.PlcDevices);
        Assert.Equal("SW_1", pair.SoftwareName);
    }

    [Fact]
    public void AcceptsRootArray()
    {
        const string tree = """
            [
              { "name": "D1", "nodeType": "Device", "children": [
                  { "name": "S1", "nodeType": "PlcSoftware", "children": [] }
              ] }
            ]
            """;

        var s = ScanProjectTreeParser.Parse(tree, 21, "V21");

        Assert.Single(s.PlcDevices);
    }

    [Fact]
    public void BadJsonYieldsUnknownProject()
    {
        var s = ScanProjectTreeParser.Parse("not json", 21, "V21");

        Assert.Equal("Unknown Project", s.Name);
        Assert.Empty(s.PlcDevices);
    }

    [Fact]
    public void SerializesCamelCaseWithMapping()
    {
        var s = ScanProjectTreeParser.Parse(CopiedDeviceTree, 21, "V21");
        s.Name = "P";

        var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);

        var first = doc.RootElement.GetProperty("plcDevices")[0];
        Assert.Equal("PLF-01A-PLC_9", first.GetProperty("deviceName").GetString());
        Assert.Equal("PLF-00A-PLC_MASTER", first.GetProperty("softwareName").GetString());
    }
}
