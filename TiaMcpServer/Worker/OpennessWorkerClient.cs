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

    public async Task<string> ReadBlockInterfaceAsync(string blockPath, string? plcName, string? projectPath, int? tiaVersion = null)
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
                    Method = "read_block_interface",
                    BlockPath = blockPath,
                    PlcName = plcName,
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? "{}"
                : $"Error: {response.Error ?? "Failed to read block interface."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> ExportPlcTypeAsync(string typeName, string? plcName, string? folderPath, string? projectPath, int? tiaVersion = null)
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
                    Method = "export_plc_type",
                    TypeName = typeName,
                    PlcName = plcName,
                    FolderPath = folderPath,
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? string.Empty
                : $"Error: {response.Error ?? "Failed to export PLC type."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> ExportTagTableXmlAsync(string? tableName, string? plcName, string? folderPath, string? projectPath, int? tiaVersion = null)
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
                    Method = "export_tag_table_xml",
                    TableName = tableName,
                    PlcName = plcName,
                    FolderPath = folderPath,
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? string.Empty
                : $"Error: {response.Error ?? "Failed to export tag table XML."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> ListConnectionsAsync(string? projectPath, int? tiaVersion = null)
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
                    Method = "list_connections",
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? "{}"
                : $"Error: {response.Error ?? "Failed to list connections."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> BrowseHmiScreensAsync(string? deviceName, string? projectPath, int? tiaVersion = null)
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
                    Method = "browse_hmi_screens",
                    DeviceName = deviceName,
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? "[]"
                : $"Error: {response.Error ?? "Failed to browse HMI screens."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> GetTiaVersionAsync(int? tiaVersion = null)
    {
        try
        {
            var response = await SendAsync(
                new WorkerRequest { Method = "get_tia_version", TiaVersion = tiaVersion }).ConfigureAwait(false);
            return response.Success
                ? response.Payload ?? "{}"
                : $"Error: {response.Error ?? "Failed to detect TIA Portal version."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> BrowseProjectTreeAsync(string? projectPath, int? tiaVersion = null)
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
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
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

    public async Task<string> ReadHardwareConfigAsync(string? projectPath, int? tiaVersion = null)
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
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
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

    public async Task<string> SearchEquipmentCatalogAsync(string query, string? projectPath, int? tiaVersion = null)
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
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
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
        string? projectPath,
        int? tiaVersion = null)
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
                    AllowTiaConfirmations = true,
                    TiaVersion = tiaVersion
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
        string? projectPath,
        int? tiaVersion = null)
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
                    AllowTiaConfirmations = true,
                    TiaVersion = tiaVersion
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

    public async Task<string> ReadCrossReferencesAsync(string? projectPath, string? plcName, string? filter, int? tiaVersion = null)
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
                    CrossReferenceFilter = normalizedFilter,
                    TiaVersion = tiaVersion
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

    public async Task<string> GetBlockContentAsync(string blockPath, string? projectPath, int? tiaVersion = null)
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
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
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

    public async Task<string> UpdateBlockLogicAsync(string blockPath, string yamlContent, string? projectPath, int? tiaVersion = null)
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
                    AllowTiaConfirmations = true,
                    TiaVersion = tiaVersion
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

    public async Task<string> ListTagTablesAsync(string? plcName, string? projectPath, int? tiaVersion = null)
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
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
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

    public async Task<string> CompileCheckAsync(string? blockPath, string? plcName, string? projectPath, int? tiaVersion = null)
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
                    Method = "compile_check",
                    BlockPath = blockPath,
                    PlcName = plcName,
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
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

    public Task<string> CreateTagTableAsync(
        string? plcName,
        string tableName,
        string? folderPath,
        string? projectPath,
        int? tiaVersion = null)
    {
        return SendBoundProjectRequestAsync(
            "create_tag_table",
            projectPath,
            request =>
            {
                request.PlcName = plcName;
                request.TableName = tableName;
                request.FolderPath = folderPath;
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);
    }

    public Task<string> DeleteTagTableAsync(
        string? plcName,
        string tableName,
        string? folderPath,
        string? projectPath,
        int? tiaVersion = null)
    {
        return SendBoundProjectRequestAsync(
            "delete_tag_table",
            projectPath,
            request =>
            {
                request.PlcName = plcName;
                request.TableName = tableName;
                request.FolderPath = folderPath;
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);
    }

    public Task<string> CreateTagAsync(
        string? plcName,
        string tableName,
        string? folderPath,
        string name,
        string dataType,
        string? logicalAddress,
        string? projectPath,
        int? tiaVersion = null)
    {
        return SendBoundProjectRequestAsync(
            "create_tag",
            projectPath,
            request =>
            {
                request.PlcName = plcName;
                request.TableName = tableName;
                request.FolderPath = folderPath;
                request.Name = name;
                request.DataType = dataType;
                request.LogicalAddress = logicalAddress;
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);
    }

    public Task<string> UpdateTagAsync(
        string? plcName,
        string tableName,
        string? folderPath,
        string name,
        string? newName,
        string? dataType,
        string? logicalAddress,
        bool? externalAccessible,
        bool? externalVisible,
        bool? externalWritable,
        bool? isSafety,
        string? projectPath,
        int? tiaVersion = null)
    {
        return SendBoundProjectRequestAsync(
            "update_tag",
            projectPath,
            request =>
            {
                request.PlcName = plcName;
                request.TableName = tableName;
                request.FolderPath = folderPath;
                request.Name = name;
                request.NewName = newName;
                request.DataType = dataType;
                request.LogicalAddress = logicalAddress;
                request.ExternalAccessible = externalAccessible;
                request.ExternalVisible = externalVisible;
                request.ExternalWritable = externalWritable;
                request.IsSafety = isSafety;
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);
    }

    public Task<string> DeleteTagAsync(
        string? plcName,
        string tableName,
        string? folderPath,
        string name,
        string? projectPath,
        int? tiaVersion = null)
    {
        return SendBoundProjectRequestAsync(
            "delete_tag",
            projectPath,
            request =>
            {
                request.PlcName = plcName;
                request.TableName = tableName;
                request.FolderPath = folderPath;
                request.Name = name;
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);
    }

    public Task<string> CreateUserConstantAsync(
        string? plcName,
        string tableName,
        string? folderPath,
        string name,
        string dataType,
        string value,
        string? projectPath,
        int? tiaVersion = null)
    {
        return SendBoundProjectRequestAsync(
            "create_user_constant",
            projectPath,
            request =>
            {
                request.PlcName = plcName;
                request.TableName = tableName;
                request.FolderPath = folderPath;
                request.Name = name;
                request.DataType = dataType;
                request.Value = value;
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);
    }

    public Task<string> UpdateUserConstantAsync(
        string? plcName,
        string tableName,
        string? folderPath,
        string name,
        string? dataType,
        string? value,
        string? projectPath,
        int? tiaVersion = null)
    {
        return SendBoundProjectRequestAsync(
            "update_user_constant",
            projectPath,
            request =>
            {
                request.PlcName = plcName;
                request.TableName = tableName;
                request.FolderPath = folderPath;
                request.Name = name;
                request.DataType = dataType;
                request.Value = value;
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);
    }

    public Task<string> DeleteUserConstantAsync(
        string? plcName,
        string tableName,
        string? folderPath,
        string name,
        string? projectPath,
        int? tiaVersion = null)
    {
        return SendBoundProjectRequestAsync(
            "delete_user_constant",
            projectPath,
            request =>
            {
                request.PlcName = plcName;
                request.TableName = tableName;
                request.FolderPath = folderPath;
                request.Name = name;
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);
    }

    public Task<string> GetProjectStatusAsync(string? projectPath, int? tiaVersion = null)
    {
        return SendBoundProjectRequestAsync(
            "get_project_status",
            projectPath,
            _ => { },
            "{}",
            tiaVersion);
    }

    public async Task<string> OpenProjectAsync(string projectPath, bool forceRebind, int? tiaVersion = null)
    {
        if (!CanBind(projectPath, forceRebind, out var bindingError))
        {
            return $"Error: {bindingError}";
        }

        try
        {
            var response = await SendAsync(
                new WorkerRequest
                {
                    Method = "open_project",
                    ProjectPath = projectPath,
                    Confirm = true,
                    ForceRebind = forceRebind,
                    AllowTiaConfirmations = true,
                    TiaVersion = tiaVersion
                }).ConfigureAwait(false);

            if (!response.Success)
            {
                return FormatWorkerError(response);
            }

            if (!_projectSessionBinding.Bind(projectPath, forceRebind, out var bindError))
            {
                return $"Error: {bindError}";
            }

            return response.Payload ?? "{}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> CreateProjectAsync(
        string projectDirectory,
        string projectName,
        string? author,
        string? comment,
        int? tiaVersion = null)
    {
        try
        {
            var response = await SendAsync(
                new WorkerRequest
                {
                    Method = "create_project",
                    ProjectDirectory = projectDirectory,
                    ProjectName = projectName,
                    Author = author,
                    Comment = comment,
                    Confirm = true,
                    AllowTiaConfirmations = true,
                    TiaVersion = tiaVersion
                }).ConfigureAwait(false);

            if (!response.Success)
            {
                return FormatWorkerError(response);
            }

            var projectPath = TryReadProjectPath(response.Payload);
            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                _projectSessionBinding.Bind(projectPath!, forceRebind: true, out _);
            }

            return response.Payload ?? "{}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public Task<string> SaveProjectAsync(string? projectPath, int? tiaVersion = null)
    {
        return SendBoundProjectRequestAsync(
            "save_project",
            projectPath,
            request =>
            {
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);
    }

    public async Task<string> SaveProjectAsAsync(
        string? projectPath,
        string targetDirectory,
        string targetName,
        bool rebind,
        int? tiaVersion = null)
    {
        var result = await SendBoundProjectRequestAsync(
            "save_project_as",
            projectPath,
            request =>
            {
                request.TargetDirectory = targetDirectory;
                request.TargetName = targetName;
                request.Rebind = rebind;
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion).ConfigureAwait(false);

        if (rebind && !result.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
        {
            var copiedProjectPath = TryReadProjectPath(result);
            if (!string.IsNullOrWhiteSpace(copiedProjectPath))
            {
                _projectSessionBinding.Bind(copiedProjectPath!, forceRebind: true, out _);
            }
        }

        return result;
    }

    public Task<string> ArchiveProjectAsync(
        string? projectPath,
        string archiveDirectory,
        string archiveName,
        string? mode,
        bool saveBeforeArchive,
        int? tiaVersion = null)
    {
        if (!ArchiveModeNames.TryNormalize(mode, out var normalizedMode, out var modeError))
        {
            return Task.FromResult($"Error: {modeError}");
        }

        return SendBoundProjectRequestAsync(
            "archive_project",
            projectPath,
            request =>
            {
                request.ArchiveDirectory = archiveDirectory;
                request.ArchiveName = archiveName;
                request.ArchiveMode = normalizedMode;
                request.SaveBeforeArchive = saveBeforeArchive;
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);
    }

    public async Task<string> CloseProjectAsync(string? projectPath, bool saveBeforeClose, int? tiaVersion = null)
    {
        var result = await SendBoundProjectRequestAsync(
            "close_project",
            projectPath,
            request =>
            {
                request.SaveBeforeClose = saveBeforeClose;
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion).ConfigureAwait(false);

        if (!result.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) &&
            _projectSessionBinding.Clear(projectPath, out _) is false)
        {
            _projectSessionBinding.Clear(null, out _);
        }

        return result;
    }

    private async Task<string> SendBoundProjectRequestAsync(
        string method,
        string? projectPath,
        Action<WorkerRequest> configure,
        string emptyPayload,
        int? tiaVersion = null)
    {
        try
        {
            if (!_projectSessionBinding.TryResolve(projectPath, out var effectiveProjectPath, out var bindingError))
            {
                return $"Error: {bindingError}";
            }

            var request = new WorkerRequest
            {
                Method = method,
                ProjectPath = effectiveProjectPath,
                TiaVersion = tiaVersion
            };
            configure(request);

            var response = await SendAsync(request).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? emptyPayload
                : FormatWorkerError(response);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    private bool CanBind(string projectPath, bool forceRebind, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            error = "Project path is required.";
            return false;
        }

        var boundProjectPath = _projectSessionBinding.BoundProjectPath;
        if (boundProjectPath is null ||
            forceRebind ||
            string.Equals(boundProjectPath, projectPath.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        error = $"This MCP session is already bound to project '{boundProjectPath}' and cannot use '{projectPath}'. Start a new MCP session for a different TIA project or set forceRebind=true.";
        return false;
    }

    private static string FormatWorkerError(WorkerResponse response)
    {
        return $"Error: {response.Error ?? "The TIA Openness worker failed without an error message."}";
    }

    private static string? TryReadProjectPath(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.TryGetProperty("projectPath", out var projectPath) &&
            projectPath.ValueKind == JsonValueKind.String)
        {
            return projectPath.GetString();
        }

        if (document.RootElement.TryGetProperty("project", out var project) &&
            project.ValueKind == JsonValueKind.Object &&
            project.TryGetProperty("path", out var statusPath) &&
            statusPath.ValueKind == JsonValueKind.String)
        {
            return statusPath.GetString();
        }

        return null;
    }

    private static async Task<WorkerResponse> SendAsync(WorkerRequest request)
    {
        // If tiaVersion not specified in the tool call, read default from file
        if (!request.TiaVersion.HasValue)
        {
            var versionFile = Environment.GetEnvironmentVariable("TIA_VERSION_FILE");
            if (!string.IsNullOrEmpty(versionFile) && File.Exists(versionFile))
            {
                var content = File.ReadAllText(versionFile).Trim();
                if (int.TryParse(content, out var defaultVersion))
                {
                    request.TiaVersion = defaultVersion;
                }
            }
        }

        var workerPath = LocateWorkerExecutable(request.TiaVersion);
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

        if (request.TiaVersion.HasValue)
        {
            startInfo.Environment["TIA_PREFERRED_VERSION"] = request.TiaVersion.Value.ToString();
        }

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

    private static string LocateWorkerExecutable(int? tiaVersion = null)
    {
        // V16 uses its own worker (compiled against V16's Siemens.Engineering.dll)
        // V18 uses the legacy worker (compiled against V18's single Siemens.Engineering.dll)
        // V21+ uses the standard worker (compiled against split DLLs)
        bool useV16 = tiaVersion.HasValue && tiaVersion.Value == 16;
        bool useLegacy = tiaVersion.HasValue && tiaVersion.Value >= 17 && tiaVersion.Value < 21;
        string workerName;
        string projectDir;

        if (useV16)
        {
            workerName = "TiaMcpServer.OpennessWorker.V16.exe";
            projectDir = "TiaMcpServer.OpennessWorker.V16";
        }
        else if (useLegacy)
        {
            workerName = "TiaMcpServer.OpennessWorker.Legacy.exe";
            projectDir = "TiaMcpServer.OpennessWorker.Legacy";
        }
        else
        {
            workerName = "TiaMcpServer.OpennessWorker.exe";
            projectDir = "TiaMcpServer.OpennessWorker";
        }

        var packagedPath = Path.Combine(AppContext.BaseDirectory, "openness-worker", workerName);
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
                    projectDir,
                    "bin",
                    configuration,
                    "net48",
                    workerName);

                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"TIA Openness worker executable was not found ({workerName} for V{tiaVersion?.ToString() ?? "auto"}). Build the solution and ensure the openness-worker folder is beside the MCP server executable.",
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
