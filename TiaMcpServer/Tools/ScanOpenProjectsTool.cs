using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class ScanOpenProjectsTool
{
    [McpServerTool(Name = "scan_open_projects")]
    [Description("Scan all installed TIA Portal versions for open projects. Returns a combined list of projects from every running TIA Portal instance across all versions (V16, V18, V21, etc.).")]
    public static async Task<string> ScanOpenProjects(
        OpennessWorkerClient workerClient)
    {
        // Step 1: Detect all installed versions (no version preference)
        var versionResult = await workerClient.GetTiaVersionAsync();
        if (versionResult.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
        {
            return versionResult;
        }

        List<VersionEntry> installedVersions;
        try
        {
            using var doc = JsonDocument.Parse(versionResult);
            var root = doc.RootElement;

            installedVersions = new List<VersionEntry>();

            // Parse installed versions array
            if (root.TryGetProperty("installedVersions", out var versionsArr))
            {
                foreach (var v in versionsArr.EnumerateArray())
                {
                    installedVersions.Add(new VersionEntry
                    {
                        MajorVersion = v.GetProperty("majorVersion").GetInt32(),
                        DisplayName = v.GetProperty("displayName").GetString() ?? ""
                    });
                }
            }
        }
        catch (Exception ex)
        {
            return $"Error: Failed to parse version data: {ex.Message}";
        }

        if (installedVersions.Count == 0)
        {
            return JsonSerializer.Serialize(new { projects = Array.Empty<object>() });
        }

        // Step 2: Browse project tree for each installed version SEQUENTIALLY.
        // Running workers in parallel causes COM contention between different
        // TIA Portal versions, making some workers hang indefinitely.
        // Sequential execution: each worker finishes in 2-5s, so the total scan
        // is still fast (~10s for 3 versions) and reliable.
        var results = new List<ScannedProject>();
        foreach (var v in installedVersions)
        {
            try
            {
                Console.Error.WriteLine($"[SCAN] Scanning {v.DisplayName}...");
                var treeResult = await workerClient.BrowseProjectTreeAsync(
                    projectPath: null, tiaVersion: v.MajorVersion);

                if (treeResult.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine($"[SCAN] {v.DisplayName}: {treeResult}");
                    results.Add(new ScannedProject
                    {
                        Error = treeResult,
                        Version = v.MajorVersion,
                        DisplayName = v.DisplayName
                    });
                }
                else
                {
                    var project = ParseProjectFromTree(treeResult, v.MajorVersion, v.DisplayName);
                    Console.Error.WriteLine($"[SCAN] {v.DisplayName}: found project with {project.DeviceCount} devices, {project.BlockCount} blocks");
                    results.Add(project);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SCAN] {v.DisplayName} exception: {ex.Message}");
                results.Add(new ScannedProject
                {
                    Error = $"Error scanning V{v.MajorVersion}: {ex.Message}",
                    Version = v.MajorVersion,
                    DisplayName = v.DisplayName
                });
            }
        }

        return JsonSerializer.Serialize(new
        {
            projects = results
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private static ScannedProject ParseProjectFromTree(string treeJson, int version, string displayName)
    {
        try
        {
            using var doc = JsonDocument.Parse(treeJson);
            var root = doc.RootElement;

            var deviceNames = new List<string>();
            var plcNames = new List<string>();
            var blockCount = 0;
            var tagTableCount = 0;

            void WalkNode(JsonElement node)
            {
                var name = node.TryGetProperty("name", out var n) ? n.GetString() : "";
                var nodeType = node.TryGetProperty("nodeType", out var nt) ? nt.GetString() : "";

                switch (nodeType)
                {
                    case "Device":
                        deviceNames.Add(name ?? "");
                        break;
                    case "PlcSoftware":
                        plcNames.Add(name ?? "");
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
                        WalkNode(child);
                    }
                }
            }

            // Handle both array and single object
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    WalkNode(item);
                }
            }
            else
            {
                WalkNode(root);
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

    private class VersionEntry
    {
        public int MajorVersion { get; set; }
        public string DisplayName { get; set; } = "";
    }

    private class ScannedProject
    {
        public string Name { get; set; } = "";
        public int Version { get; set; }
        public string DisplayName { get; set; } = "";
        public List<string> PlcNames { get; set; } = new();
        public List<string> DeviceNames { get; set; } = new();
        public int DeviceCount { get; set; }
        public int BlockCount { get; set; }
        public int TagTableCount { get; set; }
        public string? Error { get; set; }
    }
}
