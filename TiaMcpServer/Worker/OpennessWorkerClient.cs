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

    public async Task<string> ExportTagTableJsonAsync(string? tableName, string? plcName, string? folderPath, string? projectPath, int? tiaVersion = null)
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
                    Method = "export_tag_table_json",
                    TableName = tableName,
                    PlcName = plcName,
                    FolderPath = folderPath,
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? string.Empty
                : $"Error: {response.Error ?? "Failed to export tag table JSON."}";
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

    public async Task<string> BrowseHmiScreensAsync(string? deviceName, string? projectPath, string? mode = null, string? screenName = null, int? tiaVersion = null)
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
                    Mode = mode,
                    ScreenName = screenName,
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

    public async Task<string> ExportHmiScreenAsync(string deviceName, string screenName, string? projectPath, int? tiaVersion = null)
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
                    Method = "export_hmi_screen",
                    DeviceName = deviceName,
                    ScreenName = screenName,
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? ""
                : $"Error: {response.Error ?? "Failed to export HMI screen."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> ImportHmiScreenAsync(string deviceName, string screenName, string? folderPath, string xmlContent, string? projectPath, int? tiaVersion = null)
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
                    Method = "import_hmi_screen",
                    DeviceName = deviceName,
                    ScreenName = screenName,
                    FolderPath = folderPath,
                    YamlContent = xmlContent,
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? "Import succeeded"
                : $"Error: {response.Error ?? "Failed to import HMI screen."}";
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

    public async Task<string> BrowseProjectTreeAsync(
        string? projectPath,
        string? plcName = null,
        int? tiaVersion = null,
        int? maxNodes = null,
        int? skip = null,
        int? depth = null,
        string? startPath = null,
        string? cursor = null,
        TimeSpan? timeout = null)
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
                    PlcName = plcName,
                    TiaVersion = tiaVersion,
                    MaxNodes = maxNodes,
                    Skip = skip,
                    Depth = depth,
                    StartPath = startPath,
                    Cursor = cursor
                }, timeout).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? "[]"
                : $"Error: {response.Error ?? "The TIA Openness worker failed without an error message."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> ListPlcsAsync(string? projectPath, int? tiaVersion = null)
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
                    Method = "list_plcs",
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

    public async Task<string> ListBlocksAsync(string? plcName, string? projectPath, int? tiaVersion = null)
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
                    Method = "list_blocks",
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

    public async Task<string> ExtractPlcBlocksAsync(string? plcName, string? projectPath, int? tiaVersion = null)
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
                    Method = "extract_plc_blocks",
                    PlcName = plcName,
                    ProjectPath = effectiveProjectPath,
                    TiaVersion = tiaVersion
                }).ConfigureAwait(false);

            return response.Success
                ? response.Payload ?? "[]"
                : $"Error: {response.Error ?? "Failed to extract PLC blocks."}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or JsonException)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> ComparePlcBlocksAsync(
        string plcNameA, string? projectPathA, int? tiaVersionA,
        string plcNameB, string? projectPathB, int? tiaVersionB)
    {
        var jsonA = await ExtractPlcBlocksAsync(plcNameA, projectPathA, tiaVersionA).ConfigureAwait(false);
        if (jsonA.StartsWith("Error:", StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(new { error = jsonA["Error: ".Length..], side = "A" }, JsonOptions);
        }

        var jsonB = await ExtractPlcBlocksAsync(plcNameB, projectPathB, tiaVersionB).ConfigureAwait(false);
        if (jsonB.StartsWith("Error:", StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(new { error = jsonB["Error: ".Length..], side = "B" }, JsonOptions);
        }

        var sideA = JsonSerializer.Deserialize<List<BlockInfo>>(jsonA, JsonOptions) ?? new List<BlockInfo>();
        var sideB = JsonSerializer.Deserialize<List<BlockInfo>>(jsonB, JsonOptions) ?? new List<BlockInfo>();
        var diff = PlcBlockCompare.Compare(sideA, sideB);

        var result = new
        {
            summary = new
            {
                added = diff.Added.Count,
                removed = diff.Removed.Count,
                changed = diff.Changed.Count,
                unchanged = diff.Unchanged.Count,
                sideA = new { version = tiaVersionA, project = projectPathA, plc = plcNameA, total = sideA.Count },
                sideB = new { version = tiaVersionB, project = projectPathB, plc = plcNameB, total = sideB.Count },
            },
            added = diff.Added.Select(b => new { name = b.Name, type = b.Type, sourceA = b.Source }),
            removed = diff.Removed.Select(b => new { name = b.Name, type = b.Type, sourceB = b.Source }),
            changed = diff.Changed.Select(b => new { name = b.Name, type = b.Type, sourceA = b.SourceA, sourceB = b.SourceB, note = b.Note }),
            unchanged = diff.Unchanged.Select(b => new { name = b.Name, type = b.Type, sourceA = b.Source }),
        };
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    // ------------------------------------------------------------------ read_batch

    /// <summary>
    /// Run up to <see cref="ReadBatchCatalog.MaxBatchSize"/> whitelisted READ operations in one
    /// call. Each item is routed through the EXISTING per-op client method, so version routing,
    /// the persistent worker, the export fallback chain and source reconstruction all apply.
    /// Validation runs first (an invalid batch makes no worker calls); operations run
    /// sequentially with per-item isolation (one failure never aborts the rest); results are
    /// capped by <see cref="ReadBatchBudget"/> (20k chars per item, 150k per batch — later items
    /// are omitted, failures never are).
    /// </summary>
    public async Task<string> ReadBatchAsync(List<ReadBatchOperation> operations)
    {
        var validation = ReadBatchCatalog.Validate(operations);
        if (!validation.IsValid)
        {
            return JsonSerializer.Serialize(new
            {
                tool = "read_batch",
                error = "batch validation failed — no operations were executed",
                errors = validation.Errors,
            }, JsonOptions);
        }

        var results = new List<ReadBatchOperationResult>(operations.Count);
        foreach (var op in operations)
        {
            var stopwatch = Stopwatch.StartNew();
            string raw;
            try
            {
                raw = await RunReadOperation(op).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Per-item isolation: any unexpected failure becomes that item's error text.
                results.Add(new ReadBatchOperationResult
                {
                    OperationId = op.OperationId,
                    Operation = op.Operation,
                    Status = "failed",
                    Result = ex.Message,
                });
                continue;
            }

            stopwatch.Stop();
            var failed = raw.StartsWith("Error:", StringComparison.Ordinal);
            results.Add(new ReadBatchOperationResult
            {
                OperationId = op.OperationId,
                Operation = op.Operation,
                Status = failed ? "failed" : "succeeded",
                Ms = stopwatch.ElapsedMilliseconds,
                Result = failed ? raw : ReadBatchBudget.ApplyItemCap(raw),
            });
        }

        ReadBatchBudget.ApplyBatchCap(results);

        return JsonSerializer.Serialize(ReadBatchResponse.For(results), JsonOptions);
    }

    /// <summary>Dispatch one whitelisted operation to its existing client method. The catalog
    /// has already verified the required fields are present, so the null-forgiving args are
    /// safe; the default arm is unreachable but keeps the method total.</summary>
    private async Task<string> RunReadOperation(ReadBatchOperation op)
        => (op.Operation ?? "").ToLowerInvariant() switch
        {
            "get_block_content" => await GetBlockContentAsync(op.BlockPath!, op.ProjectPath, op.TiaVersion).ConfigureAwait(false),
            "read_block_interface" => await ReadBlockInterfaceAsync(op.BlockPath!, op.PlcName, op.ProjectPath, op.TiaVersion).ConfigureAwait(false),
            "list_blocks" => await ListBlocksAsync(op.PlcName, op.ProjectPath, op.TiaVersion).ConfigureAwait(false),
            "list_tag_tables" => await ListTagTablesAsync(op.PlcName, op.ProjectPath, op.TiaVersion).ConfigureAwait(false),
            "find_tags" => await FindTagsAsync(op.Query!, op.PlcName, op.ProjectPath, op.TiaVersion).ConfigureAwait(false),
            "search_code" => await SearchCodeAsync(op.Query!, ignoreCase: true, op.ContextLines ?? 2, op.PlcName, op.ProjectPath, op.TiaVersion).ConfigureAwait(false),
            "tag_usage" => await TagUsageAsync(op.Tag!, op.PlcName, op.ProjectPath, op.TiaVersion).ConfigureAwait(false),
            _ => "Error: unknown operation '" + op.Operation + "'",
        };

    public async Task<string> ListPlcTypesAsync(string? plcName, string? projectPath, int? tiaVersion = null)
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
                    Method = "list_plc_types",
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

    public async Task<string> FindTagsAsync(string query, string? plcName, string? projectPath, int? tiaVersion = null)
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
                    Method = "find_tags",
                    Query = query,
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

    public Task<string> DeleteBlockAsync(
        string blockPath,
        string? projectPath,
        int? tiaVersion = null)
    {
        return SendBoundProjectRequestAsync(
            "delete_block",
            projectPath,
            request =>
            {
                request.BlockPath = blockPath;
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);
    }

    public async Task<string> SearchCodeAsync(
        string query, bool ignoreCase, int contextLines, string? plcName, string? projectPath, int? tiaVersion = null)
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
                    Method = "search_code",
                    Query = query,
                    PlcName = plcName,
                    IgnoreCase = ignoreCase,
                    ContextLines = contextLines,
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

    public async Task<string> TagUsageAsync(string tag, string? plcName, string? projectPath, int? tiaVersion = null)
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
                    Method = "tag_usage",
                    Query = tag,
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

    public async Task<string> TagXrefAsync(string tag, string? plcName, string? projectPath, int? tiaVersion = null)
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
                    Method = "tag_xref",
                    Query = tag,
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

    public async Task<string> CallGraphAsync(string block, string? plcName, string? projectPath, int? tiaVersion = null)
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
                    Method = "call_graph",
                    Query = block,
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

    public async Task<string> HmiTagTraceAsync(
        string? deviceName, string? screenName, string? plcName, string? projectPath, int? tiaVersion = null)
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
                    Method = "hmi_tag_trace",
                    DeviceName = deviceName,
                    ScreenName = screenName,
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

    public async Task<string> KnowHowUnlockAsync(string? plcName, string? password, string? projectPath, int? tiaVersion = null)
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
                    Method = "knowhow_unlock",
                    PlcName = plcName,
                    Password = password,
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

    /// <summary>
    /// Control the PLC run state (start_plc / stop_plc). <paramref name="confirm"/> false =
    /// dry run: the worker reads the current operating state and reports what would happen;
    /// true = the transition is executed. The confirm flag is forwarded (unlike the tag
    /// mutations, which are always confirmed by the time they reach the worker).
    /// </summary>
    public Task<string> StartPlcAsync(string? plcName, bool confirm, string? projectPath, int? tiaVersion = null)
        => PlcRunStateRequestAsync("start_plc", plcName, confirm, projectPath, tiaVersion);

    public Task<string> StopPlcAsync(string? plcName, bool confirm, string? projectPath, int? tiaVersion = null)
        => PlcRunStateRequestAsync("stop_plc", plcName, confirm, projectPath, tiaVersion);

    private Task<string> PlcRunStateRequestAsync(
        string method, string? plcName, bool confirm, string? projectPath, int? tiaVersion)
    {
        return SendBoundProjectRequestAsync(
            method,
            projectPath,
            request =>
            {
                request.PlcName = plcName;
                request.Confirm = confirm;
                if (confirm)
                {
                    request.AllowTiaConfirmations = true;
                }
            },
            "{}",
            tiaVersion);
    }

    /// <summary>
    /// Block-folder lifecycle. <paramref name="confirm"/> false = preview (existence check /
    /// delete blast-radius counts); true = execute. Confirm is forwarded to the worker.
    /// </summary>
    public Task<string> CreateBlockGroupAsync(string groupPath, bool confirm, string? projectPath, int? tiaVersion = null)
        => SendBoundProjectRequestAsync(
            "create_block_group",
            projectPath,
            request =>
            {
                request.BlockPath = groupPath;
                request.Confirm = confirm;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);

    public Task<string> DeleteBlockGroupAsync(string groupPath, bool confirm, string? projectPath, int? tiaVersion = null)
        => SendBoundProjectRequestAsync(
            "delete_block_group",
            projectPath,
            request =>
            {
                request.BlockPath = groupPath;
                request.Confirm = confirm;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);

    /// <summary>Read a PLC type (UDT) rendered as a readable interface listing (reconstructed
    /// from the export XML; falls back to the raw XML with a note).</summary>
    public Task<string> GetTypeContentAsync(
        string typeName, string? plcName, string? folderPath, string? projectPath, int? tiaVersion = null)
        => SendBoundProjectRequestAsync(
            "get_type_content",
            projectPath,
            request =>
            {
                request.TypeName = typeName;
                request.PlcName = plcName;
                request.FolderPath = folderPath;
            },
            "",
            tiaVersion);

    /// <summary>Update an existing PLC type from TIA export XML (update-only: the declared
    /// &lt;Name&gt; must match and the type must exist — the worker refuses otherwise).</summary>
    public Task<string> UpdateTypeContentAsync(
        string typeName, string? plcName, string? folderPath, string xmlContent, string? projectPath, int? tiaVersion = null)
        => SendBoundProjectRequestAsync(
            "update_type_content",
            projectPath,
            request =>
            {
                request.TypeName = typeName;
                request.PlcName = plcName;
                request.FolderPath = folderPath;
                request.YamlContent = xmlContent;
                request.Confirm = true;
                request.AllowTiaConfirmations = true;
            },
            "{}",
            tiaVersion);

    /// <summary>
    /// Subnet lifecycle. <paramref name="confirm"/> false = read-only preview (create: duplicate
    /// check; update: located subnet + pending changes; delete: connected nodes). Confirm is
    /// forwarded to the worker. <paramref name="force"/> (delete only) allows deleting a subnet
    /// that still has connected nodes.
    /// </summary>
    public Task<string> CreateSubnetAsync(
        string name, string networkType, int? highestAddress, string? transmissionSpeed,
        bool confirm, string? projectPath, int? tiaVersion = null)
        => SendBoundProjectRequestAsync(
            "create_subnet",
            projectPath,
            request =>
            {
                request.SubnetName = name;
                request.SubnetNetworkType = networkType;
                request.SubnetHighestAddress = highestAddress;
                request.SubnetTransmissionSpeed = transmissionSpeed;
                request.Confirm = confirm;
                if (confirm)
                {
                    request.AllowTiaConfirmations = true;
                }
            },
            "{}",
            tiaVersion);

    public Task<string> UpdateSubnetAsync(
        string name, string? newName, int? highestAddress, string? transmissionSpeed,
        bool confirm, string? projectPath, int? tiaVersion = null)
        => SendBoundProjectRequestAsync(
            "update_subnet",
            projectPath,
            request =>
            {
                request.SubnetName = name;
                request.NewName = newName;
                request.SubnetHighestAddress = highestAddress;
                request.SubnetTransmissionSpeed = transmissionSpeed;
                request.Confirm = confirm;
                if (confirm)
                {
                    request.AllowTiaConfirmations = true;
                }
            },
            "{}",
            tiaVersion);

    public Task<string> DeleteSubnetAsync(
        string name, bool confirm, bool force, string? projectPath, int? tiaVersion = null)
        => SendBoundProjectRequestAsync(
            "delete_subnet",
            projectPath,
            request =>
            {
                request.SubnetName = name;
                request.Confirm = confirm;
                request.Force = force;
                if (confirm)
                {
                    request.AllowTiaConfirmations = true;
                }
            },
            "{}",
            tiaVersion);

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

    public async Task<string> GetBlockContentAsync(string blockPath, string? projectPath, int? tiaVersion = null, bool forceRefresh = false, bool raw = false)
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
                    TiaVersion = tiaVersion,
                    ForceRefresh = forceRefresh,
                    Raw = raw
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

    // --- Persistent worker process -------------------------------------------------------
    // The worker's Main() loops on stdin (one request line -> one response line), so a single
    // process can serve many requests. Keeping it alive lets the worker's STATIC caches
    // (CodeIndexCache, WorkerCache) persist across calls, so tag_usage/search_code/hmi_tag_trace
    // don't re-export every block on every call. OpennessWorkerClient is a singleton, so this one
    // worker is shared. Access is serialized (TIA Openness is not concurrency-safe).
    private readonly SemaphoreSlim _workerLock = new(1, 1);
    private WorkerHandle? _worker;

    private sealed class WorkerHandle : IDisposable
    {
        public WorkerHandle(Process process, int tiaVersion)
        {
            Process = process;
            TiaVersion = tiaVersion;
        }

        public Process Process { get; }

        /// <summary>TIA version this worker was spawned for (0 = auto-detect / V21 worker).</summary>
        public int TiaVersion { get; }

        public bool Dead { get; set; }

        public void Dispose()
        {
            TryKill(Process);
            Process.Dispose();
        }
    }

    /// <summary>Snapshot of the persistent worker process status (for diagnostics and the
    /// <c>worker_status</c> MCP tool). No TIA Portal interaction is needed to produce it.</summary>
    public sealed record WorkerStatus(bool IsAlive, int TiaVersion, bool EverStarted);

    /// <summary>Pure status evaluation (unit-tested). A worker counts as alive only when one
    /// exists, is not flagged dead, and has not exited. The version is reported only when alive
    /// (a dead worker's version is meaningless and could mislead callers).</summary>
    internal static WorkerStatus EvaluateStatus(bool hasWorker, bool isDead, bool hasExited, int tiaVersion)
    {
        var alive = hasWorker && !isDead && !hasExited;
        return new WorkerStatus(alive, alive ? tiaVersion : 0, hasWorker);
    }

    /// <summary>Current persistent-worker status. Does NOT touch TIA Portal — reads only the
    /// cached worker handle.</summary>
    public WorkerStatus GetStatus()
    {
        var worker = _worker;
        if (worker is null)
        {
            return EvaluateStatus(false, false, false, 0);
        }

        return EvaluateStatus(true, worker.Dead, SafelyHasExited(worker.Process), worker.TiaVersion);
    }

    internal static int ResolveTiaVersion(int? requested)
    {
        if (requested.HasValue)
        {
            Console.Error.WriteLine($"[TIA-VERSION] tiaVersion={requested.Value} from tool call argument");
            return requested.Value;
        }

        var versionFile = Environment.GetEnvironmentVariable("TIA_VERSION_FILE");
        Console.Error.WriteLine($"[TIA-VERSION] tiaVersion not set in tool call. TIA_VERSION_FILE env={versionFile ?? "(null)"}");
        if (!string.IsNullOrEmpty(versionFile) && File.Exists(versionFile))
        {
            var content = File.ReadAllText(versionFile).Trim();
            Console.Error.WriteLine($"[TIA-VERSION] Read version file content: '{content}'");
            if (int.TryParse(content, out var defaultVersion))
            {
                Console.Error.WriteLine($"[TIA-VERSION] Set tiaVersion={defaultVersion} from file");
                return defaultVersion;
            }
        }
        else if (string.IsNullOrEmpty(versionFile))
        {
            Console.Error.WriteLine("[TIA-VERSION] TIA_VERSION_FILE env var not set - falling back to auto-detect");
        }
        else
        {
            Console.Error.WriteLine($"[TIA-VERSION] Version file not found at: {versionFile} - falling back to auto-detect");
        }

        return 0; // 0 = auto-detect
    }

    private WorkerHandle GetOrStartWorker(int tiaVersion)
    {
        if (_worker is { Dead: false } live
            && live.TiaVersion == tiaVersion
            && !SafelyHasExited(live.Process))
        {
            return live;
        }

        // (Re)start: dispose any dead / version-mismatched worker, then spawn a fresh one.
        var old = _worker;
        _worker = null;
        try { old?.Dispose(); } catch { /* ignore */ }

        _worker = StartWorker(tiaVersion);
        return _worker;
    }

    private WorkerHandle StartWorker(int tiaVersion)
    {
        var workerPath = LocateWorkerExecutable(tiaVersion == 0 ? null : tiaVersion);
        Console.Error.WriteLine($"[TIA-VERSION] Worker path: {workerPath}");
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

        if (tiaVersion != 0)
        {
            startInfo.Environment["TIA_PREFERRED_VERSION"] = tiaVersion.ToString();
        }

        var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Failed to start the TIA Openness worker process.");

        // Drain stderr continuously so the pipe buffer never deadlocks the worker.
        _ = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await process.StandardError.ReadLineAsync().ConfigureAwait(false)) is not null)
                {
                    Console.Error.WriteLine("[TIA worker] " + line);
                }
            }
            catch { /* worker exited */ }
        });

        return new WorkerHandle(process, tiaVersion);
    }

    private static bool SafelyHasExited(Process process)
    {
        try { return process.HasExited; }
        catch { return true; }
    }

    private static async Task<string?> ReadLineWithTimeoutAsync(StreamReader reader, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var readTask = reader.ReadLineAsync();
        var completed = await Task.WhenAny(readTask, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token))
            .ConfigureAwait(false);
        if (completed != readTask)
        {
            return null; // timed out
        }

        return await readTask.ConfigureAwait(false);
    }

    private async Task<WorkerResponse> SendAsync(WorkerRequest request, TimeSpan? timeout = null)
    {
        var tiaVersion = ResolveTiaVersion(request.TiaVersion);
        request.TiaVersion = tiaVersion == 0 ? null : tiaVersion;
        // Per-call timeout override (e.g. the fast scan timeout). Defaults to the
        // long worker timeout sized for heavy writes/compiles.
        var effectiveTimeout = timeout ?? WorkerTimeout;

        await _workerLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var worker = GetOrStartWorker(tiaVersion);

            string? responseLine;
            try
            {
                await worker.Process.StandardInput.WriteLineAsync(
                    JsonSerializer.Serialize(request, JsonOptions)).ConfigureAwait(false);
                await worker.Process.StandardInput.FlushAsync().ConfigureAwait(false);
                responseLine = await ReadLineWithTimeoutAsync(
                    worker.Process.StandardOutput, effectiveTimeout).ConfigureAwait(false);
            }
            catch (IOException)
            {
                responseLine = null; // pipe broke -> treat the worker as dead below
            }

            if (string.IsNullOrWhiteSpace(responseLine))
            {
                var exited = SafelyHasExited(worker.Process);
                worker.Dead = true;
                if (ReferenceEquals(_worker, worker))
                {
                    _worker = null;
                }
                try { worker.Dispose(); } catch { /* ignore */ }

                throw exited
                    ? new InvalidOperationException("The TIA Openness worker exited unexpectedly (TIA Portal may have been closed). Please retry.")
                    : new InvalidOperationException($"The TIA Openness worker did not respond within {(int)effectiveTimeout.TotalSeconds} s.");
            }

            var response = JsonSerializer.Deserialize<WorkerResponse>(responseLine, JsonOptions);
            return response ?? throw new InvalidOperationException("The TIA Openness worker returned an empty response.");
        }
        finally
        {
            _workerLock.Release();
        }
    }

    /// <summary>Map a TIA major version to the worker exe name + its source project dir. Pure (no
    /// filesystem) so the version-routing is unit-testable. V16 -> V16 worker (own Siemens DLLs);
    /// V17-V20 -> Legacy worker (single Siemens.Engineering.dll); V21+ and auto-detect (null) ->
    /// standard worker (split Siemens DLLs).</summary>
    internal static (string WorkerName, string ProjectDir) WorkerIdentityForVersion(int? tiaVersion)
    {
        if (tiaVersion.HasValue && tiaVersion.Value == 16)
        {
            return ("TiaMcpServer.OpennessWorker.V16.exe", "TiaMcpServer.OpennessWorker.V16");
        }

        if (tiaVersion.HasValue && tiaVersion.Value >= 17 && tiaVersion.Value < 21)
        {
            return ("TiaMcpServer.OpennessWorker.Legacy.exe", "TiaMcpServer.OpennessWorker.Legacy");
        }

        return ("TiaMcpServer.OpennessWorker.exe", "TiaMcpServer.OpennessWorker");
    }

    private static string LocateWorkerExecutable(int? tiaVersion = null)
    {
        var (workerName, projectDir) = WorkerIdentityForVersion(tiaVersion);
        Console.Error.WriteLine($"[TIA-VERSION] Worker path resolving: {workerName} ({projectDir})");

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
