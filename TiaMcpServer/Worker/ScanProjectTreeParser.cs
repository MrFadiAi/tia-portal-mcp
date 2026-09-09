using System.Text.Json;

namespace TiaMcpServer.Worker;

/// <summary>
/// One PLC of a scanned project, carrying BOTH names:
/// the hardware DEVICE name (what tools key on — browse_project_tree paths, call_graph,
/// block paths) and the PLC SOFTWARE name (what users see and know — a copied device
/// keeps the stale software name of its source, so the two can differ, e.g.
/// device "PLF-01A-PLC_9" containing software "PLF-00A-PLC_MASTER").
/// </summary>
public sealed record PlcDevicePair(string DeviceName, string SoftwareName);

/// <summary>Summary of one open project produced by <c>scan_open_projects</c>.</summary>
public sealed class ScannedProject
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string DisplayName { get; set; } = "";
    public List<string> PlcNames { get; set; } = new();
    public List<string> DeviceNames { get; set; } = new();
    /// <summary>Device↔software mapping per PLC (aligned with <see cref="PlcNames"/> by tree order).</summary>
    public List<PlcDevicePair> PlcDevices { get; set; } = new();
    public int DeviceCount { get; set; }
    public int BlockCount { get; set; }
    public int TagTableCount { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Pure (Siemens-free, MCP-free) parser from a BrowseProjectTree JSON to a
/// <see cref="ScannedProject"/> — link-compiled into the test project.
/// The two flat lists (PlcNames = every PlcSoftware node, DeviceNames = every Device
/// node, PLC and HMI mixed) are NOT index-parallel; the device↔software pairing is
/// recovered from the tree hierarchy by tracking the owning Device while walking.
/// </summary>
public static class ScanProjectTreeParser
{
    public static ScannedProject Parse(string treeJson, int version, string displayName)
    {
        try
        {
            using var doc = JsonDocument.Parse(treeJson);
            var root = doc.RootElement;

            var deviceNames = new List<string>();
            var plcNames = new List<string>();
            var plcDevices = new List<PlcDevicePair>();
            var seenPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var blockCount = 0;
            var tagTableCount = 0;

            void WalkNode(JsonElement node, string? ownerDevice)
            {
                var name = node.TryGetProperty("name", out var n) ? n.GetString() : "";
                var nodeType = node.TryGetProperty("nodeType", out var nt) ? nt.GetString() : "";
                var device = ownerDevice;

                switch (nodeType)
                {
                    case "Device":
                        deviceNames.Add(name ?? "");
                        if (!string.IsNullOrWhiteSpace(name)) device = name;
                        break;
                    case "PlcSoftware":
                        plcNames.Add(name ?? "");
                        // Pair with the owning Device when the walk is inside one; a software
                        // node outside any Device (shouldn't happen, defensive) self-pairs.
                        var dev = string.IsNullOrWhiteSpace(device) ? name : device;
                        if (!string.IsNullOrWhiteSpace(name) &&
                            !string.IsNullOrWhiteSpace(dev) &&
                            seenPairs.Add($"{dev}|{name}"))
                        {
                            plcDevices.Add(new PlcDevicePair(dev!, name!));
                        }
                        break;
                    case "OB": case "FB": case "FC":
                    case "GlobalDB": case "InstanceDB": case "ArrayDB":
                        blockCount++;
                        break;
                    case "TagTable":
                        tagTableCount++;
                        break;
                }

                if (node.TryGetProperty("children", out var children))
                {
                    foreach (var child in children.EnumerateArray())
                    {
                        WalkNode(child, device);
                    }
                }
            }

            // Handle both array and single object
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    WalkNode(item, null);
                }
            }
            else
            {
                WalkNode(root, null);
            }

            var projectName = deviceNames.Count > 0
                ? string.Join(", ", deviceNames)
                : "TIA Portal Project";

            return new ScannedProject
            {
                Name = projectName,
                Version = version,
                DisplayName = displayName,
                PlcNames = plcNames,
                DeviceNames = deviceNames,
                PlcDevices = plcDevices,
                DeviceCount = deviceNames.Count,
                BlockCount = blockCount,
                TagTableCount = tagTableCount
            };
        }
        catch
        {
            return new ScannedProject
            {
                Name = "Unknown Project",
                Version = version,
                DisplayName = displayName
            };
        }
    }
}
