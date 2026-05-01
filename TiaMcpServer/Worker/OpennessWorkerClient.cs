using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.Worker;

public class OpennessWorkerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan WorkerTimeout = TimeSpan.FromMinutes(5);

    private readonly ProjectSessionBinding _projectSessionBinding;

    public OpennessWorkerClient(ProjectSessionBinding projectSessionBinding)
    {
        _projectSessionBinding = projectSessionBinding;
    }

    public async Task<string> BrowseProjectTreeAsync(string? projectPath)
    {
        try
        {
            if (!_projectSessionBinding.TryResolve(projectPath, out var effectiveProjectPath, out var bindingError))
            {
                return $"Error: {bindingError}";
            }

            var response = await SendAsync(
                new WorkerRequest
                {
                    Method = "browse_project_tree",
                    ProjectPath = effectiveProjectPath
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? "[]"
                : $"Error: {response.Error ?? "The TIA Openness worker failed without an error message."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> ReadHardwareConfigAsync(string? projectPath)
    {
        try
        {
            if (!_projectSessionBinding.TryResolve(projectPath, out var effectiveProjectPath, out var bindingError))
            {
                return $"Error: {bindingError}";
            }

            var response = await SendAsync(
                new WorkerRequest
                {
                    Method = "read_hardware_config",
                    ProjectPath = effectiveProjectPath
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? "{}"
                : $"Error: {response.Error ?? "The TIA Openness worker failed without an error message."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> SearchEquipmentCatalogAsync(string query, string? projectPath)
    {
        try
        {
            if (!_projectSessionBinding.TryResolve(projectPath, out var effectiveProjectPath, out var bindingError))
            {
                return $"Error: {bindingError}";
            }

            var response = await SendAsync(
                new WorkerRequest
                {
                    Method = "search_equipment_catalog",
                    Query = query,
                    ProjectPath = effectiveProjectPath
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? "[]"
                : $"Error: {response.Error ?? "The TIA Openness worker failed without an error message."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> AddNetworkDeviceAsync(
        string typeIdentifier,
        string deviceName,
        string deviceItemName,
        string? projectPath)
    {
        try
        {
            if (!_projectSessionBinding.TryResolve(projectPath, out var effectiveProjectPath, out var bindingError))
            {
                return $"Error: {bindingError}";
            }

            var response = await SendAsync(
                new WorkerRequest
                {
                    Method = "add_network_device",
                    TypeIdentifier = typeIdentifier,
                    DeviceName = deviceName,
                    DeviceItemName = deviceItemName,
                    ProjectPath = effectiveProjectPath,
                    Confirm = true,
                    AllowTiaConfirmations = true
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? "{}"
                : $"Error: {response.Error ?? "The TIA Openness worker failed without an error message."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> ConfigureNetworkDeviceAsync(
        string deviceName,
        string? ipAddress,
        string? subnetMask,
        string? pnDeviceName,
        string? subnetName,
        string? ioSystemName,
        string? projectPath)
    {
        try
        {
            if (!_projectSessionBinding.TryResolve(projectPath, out var effectiveProjectPath, out var bindingError))
            {
                return $"Error: {bindingError}";
            }

            var response = await SendAsync(
                new WorkerRequest
                {
                    Method = "configure_network_device",
                    DeviceName = deviceName,
                    IpAddress = ipAddress,
                    SubnetMask = subnetMask,
                    PnDeviceName = pnDeviceName,
                    SubnetName = subnetName,
                    IoSystemName = ioSystemName,
                    ProjectPath = effectiveProjectPath,
                    Confirm = true,
                    AllowTiaConfirmations = true
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? "{}"
                : $"Error: {response.Error ?? "The TIA Openness worker failed without an error message."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> ReadCrossReferencesAsync(string? projectPath, string? plcName, string? filter)
    {
        try
        {
            if (!_projectSessionBinding.TryResolve(projectPath, out var effectiveProjectPath, out var bindingError))
            {
                return $"Error: {bindingError}";
            }

            if (!CrossReferenceFilterNames.TryNormalize(filter, out var normalizedFilter, out var filterError))
            {
                return $"Error: {filterError}";
            }

            var response = await SendAsync(
                new WorkerRequest
                {
                    Method = "read_cross_references",
                    ProjectPath = effectiveProjectPath,
                    PlcName = plcName,
                    CrossReferenceFilter = normalizedFilter
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? "{}"
                : $"Error: {response.Error ?? "The TIA Openness worker failed without an error message."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> GetBlockContentAsync(string blockPath, string? projectPath)
    {
        try
        {
            if (!_projectSessionBinding.TryResolve(projectPath, out var effectiveProjectPath, out var bindingError))
            {
                return $"Error: {bindingError}";
            }

            var response = await SendAsync(
                new WorkerRequest
                {
                    Method = "get_block_content",
                    BlockPath = blockPath,
                    ProjectPath = effectiveProjectPath
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? string.Empty
                : $"Error: {response.Error ?? "The TIA Openness worker failed without an error message."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> UpdateBlockLogicAsync(string blockPath, string yamlContent, string? projectPath)
    {
        try
        {
            if (!_projectSessionBinding.TryResolve(projectPath, out var effectiveProjectPath, out var bindingError))
            {
                return $"Error: {bindingError}";
            }

            var response = await SendAsync(
                new WorkerRequest
                {
                    Method = "update_block_logic",
                    BlockPath = blockPath,
                    YamlContent = yamlContent,
                    ProjectPath = effectiveProjectPath,
                    AllowTiaConfirmations = true
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? string.Empty
                : $"Error: {response.Error ?? "The TIA Openness worker failed without an error message."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> ListTagTablesAsync(string? plcName, string? projectPath)
    {
        try
        {
            if (!_projectSessionBinding.TryResolve(projectPath, out var effectiveProjectPath, out var bindingError))
            {
                return $"Error: {bindingError}";
            }

            var response = await SendAsync(
                new WorkerRequest
                {
                    Method = "list_tag_tables",
                    PlcName = plcName,
                    ProjectPath = effectiveProjectPath
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? "[]"
                : $"Error: {response.Error ?? "The TIA Openness worker failed without an error message."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static async Task<WorkerResponse> SendAsync(WorkerRequest request)
    {
        var workerPath = LocateWorkerExecutable();
        var startInfo = new ProcessStartInfo
        {
            FileName = workerPath,
            WorkingDirectory = Path.GetDirectoryName(workerPath) ?? AppContext.BaseDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Failed to start the TIA Openness worker process.");

        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions)).ConfigureAwait(false);
        process.StandardInput.Close();

        using var timeout = new CancellationTokenSource(WorkerTimeout);
        var responseLineTask = process.StandardOutput.ReadLineAsync();
        var completed = await Task.WhenAny(responseLineTask, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token))
            .ConfigureAwait(false);

        if (completed != responseLineTask)
        {
            TryKill(process);
            throw new TimeoutException($"TIA Openness worker did not respond within {WorkerTimeout.TotalMinutes:N0} minutes.");
        }

        var responseLine = await responseLineTask.ConfigureAwait(false);
        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(responseLine))
        {
            var detail = string.IsNullOrWhiteSpace(stderr) ? "No response was written." : stderr.Trim();
            throw new InvalidOperationException($"TIA Openness worker exited without a response. {detail}");
        }

        var response = JsonSerializer.Deserialize<WorkerResponse>(responseLine, JsonOptions);
        return response ?? throw new InvalidOperationException("TIA Openness worker returned an empty response.");
    }

    private static string LocateWorkerExecutable()
    {
        var packagedPath = Path.Combine(AppContext.BaseDirectory, "openness-worker", "TiaMcpServer.OpennessWorker.exe");
        if (File.Exists(packagedPath))
        {
            return packagedPath;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            foreach (var configuration in new[] { "Debug", "Release" })
            {
                var candidatePath = Path.Combine(
                    directory.FullName,
                    "TiaMcpServer.OpennessWorker",
                    "bin",
                    configuration,
                    "net48",
                    "TiaMcpServer.OpennessWorker.exe");

                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "TIA Openness worker executable was not found. Build the solution and ensure the openness-worker folder is beside the MCP server executable.",
            packagedPath);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
