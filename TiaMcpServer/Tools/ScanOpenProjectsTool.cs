using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class ScanOpenProjectsTool
{
    // Per-version scan timeout. The worker's default timeout (5 min) is sized for
    // heavy writes/compiles, but a browse should finish in 2-5s; if one version's
    // enumeration stalls (e.g. TIA Portal has a modal open), fail it fast so the
    // other versions' results still show instead of freezing the whole scan.
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(60);

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
                    projectPath: null, tiaVersion: v.MajorVersion, timeout: ScanTimeout);

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
                    var project = ScanProjectTreeParser.Parse(treeResult, v.MajorVersion, v.DisplayName);
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

    private class VersionEntry
    {
        public int MajorVersion { get; set; }
        public string DisplayName { get; set; } = "";
    }
}
