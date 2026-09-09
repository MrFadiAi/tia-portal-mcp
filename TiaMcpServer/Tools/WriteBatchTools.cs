using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Server;
using TiaMcpServer.Safety;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

/// <summary>
/// Grouped-write batch tools. The batch layer GROUPS the existing per-op safety rather than
/// replacing it: every operation in the catalog drives the SAME per-op client methods the
/// individual tools drive (including the worker-side confirm gates — block groups run their
/// dry-run preview when read and their execute path with confirm=true when applied), and the
/// ONE batch-level safetyToken binds what the per-op tokens bind, composed: the EXACT ordered
/// operation list plus a fingerprint over every op's preview-state payload. No per-op gate is
/// weakened — the per-op state-binding guarantee (state at apply must equal state at preview)
/// is enforced for the WHOLE batch by the composed fingerprint, and the per-op worker-side
/// validation (duplicate/not-found/etc.) still runs inside every item.
/// </summary>
[McpServerToolType]
public static class WriteBatchTools
{
    private const string PreviewToolName = "preview_write_batch";
    private const string ApplyToolName = "apply_write_batch";

    private const string StoppedOnFailureWarning =
        "WARNING: this operation and any earlier operation in the same call may already have changed TIA state — " +
        "re-read with read_batch before retrying rather than blindly re-running.";

    [McpServerTool(Name = "preview_write_batch", ReadOnly = true)]
    [Description(
        "Preview up to 10 write operations in ONE call and return ONE batch-level safetyToken bound to the " +
        "exact ordered operation list and the combined current state of everything it touches. The token is " +
        "single-use and expires after 10 minutes; pass it to apply_write_batch with the IDENTICAL operations " +
        "list. Each item's preview reuses the operation's own dry-run path (tag ops: the current tag tables; " +
        "block groups: the worker dry run). All operations must target the same project. Valid operations " +
        "(parentheses list required fields): create_tag (tableName, name, dataType), update_tag (tableName, " +
        "name), delete_tag (tableName, name), create_tag_table (tableName), delete_tag_table (tableName), " +
        "create_user_constant (tableName, name, dataType, value), update_user_constant (tableName, name), " +
        "delete_user_constant (tableName, name), create_block_group (groupPath), delete_block_group " +
        "(groupPath). NOT batchable (use their own tools with their own previews/tokens): " +
        "update_block_logic, update_type_content, create_subnet, update_subnet, delete_subnet, start_plc, " +
        "stop_plc.")]
    public static async Task<string> PreviewWriteBatch(
        OpennessWorkerClient workerClient,
        [Description("Ordered write operations (1-10). Each: { operationId, operation, ...operation parameters }.")]
        List<WriteBatchOperation> operations)
    {
        var validation = WriteBatchCatalog.Validate(operations);
        if (!validation.IsValid)
        {
            return Error(PreviewToolName, "batch validation failed — no operations were executed",
                validation.Errors);
        }

        var projectPath = WriteBatchCatalog.ResolveProjectPath(operations!, out _);
        var stateResult = await ReadOpStatesAsync(workerClient, operations!).ConfigureAwait(false);
        if (stateResult.Error is not null)
        {
            return Error(PreviewToolName, stateResult.Error, null, stateResult.FailedOperationId);
        }

        var previews = stateResult.States!
            .Select(s => new
            {
                s.OperationId,
                s.Operation,
                status = "previewed",
                result = ReadBatchBudget.ApplyItemCap(s.State),
            })
            .ToList();

        var fingerprint = WriteBatchSnapshot.ComposeStateFingerprint(
            stateResult.States.Select(s => (s.OperationId, s.State)).ToList());
        var previewJson = WriteSafetyTooling.CreatePreview(
            ApplyToolName,
            projectPath,
            WriteBatchSnapshot.BuildTargets(operations!),
            $"Apply {operations!.Count} write operation(s) sequentially; stops on first failure (no rollback, " +
            "no partial undo). The current-state snapshot is read per item and is not an atomic point-in-time view.",
            operations!,
            fingerprint);

        if (previewJson.StartsWith("Could not read current state", StringComparison.Ordinal))
        {
            return Error(PreviewToolName, previewJson, null);
        }

        // Embed the minted preview (token + hashes) verbatim under "safety" so the caller reviews
        // exactly what the token binds.
        return JsonSerializer.Serialize(new
        {
            tool = PreviewToolName,
            operationCount = operations.Count,
            previews,
            safety = JsonDocument.Parse(previewJson).RootElement,
        }, JsonOptions);
    }

    [McpServerTool(Name = "apply_write_batch", Destructive = true)]
    [Description(
        "Apply a previewed batch of write operations sequentially, STOPPING on the first failure (later items " +
        "are skipped; there is no rollback). Requires confirm=true and the single-use safetyToken from " +
        "preview_write_batch, plus the IDENTICAL ordered operations list: the token rejects any added, " +
        "removed, or reordered operation, and rejects the batch if the project state changed since the " +
        "preview. A failed item carries a warning that earlier operations may already have changed state. " +
        "Valid operations: create_tag, update_tag, delete_tag, create_tag_table, delete_tag_table, " +
        "create_user_constant, update_user_constant, delete_user_constant, create_block_group, " +
        "delete_block_group (same required fields as preview_write_batch).")]
    public static async Task<string> ApplyWriteBatch(
        OpennessWorkerClient workerClient,
        [Description("Ordered write operations. Must be the IDENTICAL list passed to preview_write_batch.")]
        List<WriteBatchOperation> operations,
        [Description("Set to true to confirm the write operations. Required safety flag; operation is rejected when false.")]
        bool confirm = false,
        [Description("Single-use safetyToken returned by preview_write_batch for this exact batch.")]
        string? safetyToken = null)
    {
        if (!confirm)
        {
            return Error(ApplyToolName, "Operation not confirmed. Set confirm=true to proceed with applying the write batch.");
        }

        var validation = WriteBatchCatalog.Validate(operations);
        if (!validation.IsValid)
        {
            return Error(ApplyToolName, "batch validation failed — no operations were executed", validation.Errors);
        }

        if (string.IsNullOrWhiteSpace(safetyToken))
        {
            return Error(ApplyToolName,
                $"Safety token required. Call {PreviewToolName} first, review the preview, then pass its safetyToken with confirm=true.");
        }

        var projectPath = WriteBatchCatalog.ResolveProjectPath(operations!, out _);

        // Re-read the combined current state and validate+consume the batch token against it.
        // ValidateForApplyAsync treats the fingerprint as the current-state payload: any op's
        // state change, or any list tampering, fails here BEFORE the first write executes.
        var stateResult = await ReadOpStatesAsync(workerClient, operations!).ConfigureAwait(false);
        if (stateResult.Error is not null)
        {
            return Error(ApplyToolName, $"Could not read current state before write. {stateResult.Error}", null, stateResult.FailedOperationId);
        }

        var fingerprint = WriteBatchSnapshot.ComposeStateFingerprint(
            stateResult.States!.Select(s => (s.OperationId, s.State)).ToList());
        var safety = await WriteSafetyTooling.ValidateForApplyAsync(
            safetyToken,
            PreviewToolName,
            ApplyToolName,
            projectPath,
            WriteBatchSnapshot.BuildTargets(operations!),
            operations!,
            () => Task.FromResult(fingerprint)).ConfigureAwait(false);
        if (!safety.IsValid)
        {
            return Error(ApplyToolName, safety.Error!);
        }

        var batchId = "wb-" + Guid.NewGuid().ToString("N")[..8];
        var results = new List<ReadBatchOperationResult>(operations!.Count);
        string? stoppedOn = null;
        string? compileCheck = null;

        for (var i = 0; i < operations.Count; i++)
        {
            var op = operations[i];
            if (stoppedOn is not null)
            {
                results.Add(Item(op, "skipped",
                    $"skipped — operation '{stoppedOn}' failed earlier in this batch and the run stopped there"));
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            string raw;
            try
            {
                raw = await RunWriteOperation(workerClient, op, projectPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Per-item isolation: an unexpected throw becomes that item's failure (and stops the run).
                raw = $"Error: {ex.Message}";
            }

            stopwatch.Stop();
            if (IsFailure(raw))
            {
                stoppedOn = op.OperationId;
                // The warning leads the result so payload caps can never cut it off.
                results.Add(Item(op, "failed", StoppedOnFailureWarning + "\n" + raw, stopwatch.ElapsedMilliseconds));
            }
            else
            {
                results.Add(Item(op, "succeeded", raw, stopwatch.ElapsedMilliseconds));
            }
        }

        // One compile check at the end (not per item): it verifies the batch's END state, which is
        // what matters after a sequential run, and keeps worker load at one compile per batch.
        if (results.Any(r => r.Status == "succeeded"))
        {
            var firstPlc = operations.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.PlcName))?.PlcName;
            var tiaVersion = operations.FirstOrDefault(o => o.TiaVersion.HasValue)?.TiaVersion;
            compileCheck = await workerClient.CompileCheckAsync(null, firstPlc, projectPath, tiaVersion).ConfigureAwait(false);
        }

        return JsonSerializer.Serialize(new
        {
            tool = ApplyToolName,
            batchId,
            operationCount = operations.Count,
            succeeded = results.Count(r => r.Status == "succeeded"),
            failed = results.Count(r => r.Status == "failed"),
            skipped = results.Count(r => r.Status == "skipped"),
            stoppedOn,
            compileCheck,
            operations = results.Select(r => new
            {
                r.OperationId, r.Operation, r.Status, r.Ms, r.Result,
                warnings = r.Warnings,
            }).ToList(),
        }, JsonOptions);
    }

    private static bool IsFailure(string raw)
        => raw.StartsWith("Error:", StringComparison.OrdinalIgnoreCase);

    private static ReadBatchOperationResult Item(
        WriteBatchOperation op, string status, string result, long ms = 0)
        => new()
        {
            OperationId = op.OperationId,
            Operation = op.Operation,
            Status = status,
            Ms = ms,
            Result = ReadBatchBudget.ApplyItemCap(result),
        };

    /// <summary>Read each op's preview state SEQUENTIALLY (order matters for the fingerprint),
    /// deduplicating identical tag-table reads. Any read failure aborts the whole phase with the
    /// failing operationId — the same "could not read current state" semantics as per-op previews.</summary>
    private static async Task<(List<(string OperationId, string Operation, string State)>? States, string? Error, string? FailedOperationId)>
        ReadOpStatesAsync(OpennessWorkerClient workerClient, List<WriteBatchOperation> operations)
    {
        var states = new List<(string, string, string)>(operations.Count);
        var tagReadCache = new Dictionary<string, string>();

        foreach (var op in operations)
        {
            string state;
            if (WriteBatchCatalog.IsBlockGroupOperation(op.Operation))
            {
                state = op.Operation.ToLowerInvariant() == "delete_block_group"
                    ? await workerClient.DeleteBlockGroupAsync(op.GroupPath!, confirm: false, op.ProjectPath, op.TiaVersion).ConfigureAwait(false)
                    : await workerClient.CreateBlockGroupAsync(op.GroupPath!, confirm: false, op.ProjectPath, op.TiaVersion).ConfigureAwait(false);
            }
            else
            {
                var cacheKey = string.Join("|",
                    op.PlcName ?? "", WriteSafetyService.NormalizeProjectPath(op.ProjectPath), op.TiaVersion?.ToString() ?? "");
                if (!tagReadCache.TryGetValue(cacheKey, out state!))
                {
                    state = await workerClient.ListTagTablesAsync(op.PlcName, op.ProjectPath, op.TiaVersion).ConfigureAwait(false);
                    tagReadCache[cacheKey] = state;
                }
            }

            if (IsFailure(state))
            {
                return (null,
                    $"Could not read current state for operationId '{op.OperationId}' ({op.Operation}). {state}",
                    op.OperationId);
            }

            states.Add((op.OperationId, op.Operation, state));
        }

        return (states, null, null);
    }

    /// <summary>Dispatch one catalog-validated operation to its existing per-op client method —
    /// the exact methods the individual tools call, worker-side confirm gates included (tag-family
    /// methods pass Confirm=true internally; block groups are driven with confirm=true).</summary>
    private static Task<string> RunWriteOperation(
        OpennessWorkerClient workerClient, WriteBatchOperation op, string? projectPath)
        => op.Operation.ToLowerInvariant() switch
        {
            "create_tag" => workerClient.CreateTagAsync(op.PlcName, op.TableName!, op.FolderPath, op.Name!, op.DataType!, op.LogicalAddress, projectPath, op.TiaVersion),
            "update_tag" => workerClient.UpdateTagAsync(op.PlcName, op.TableName!, op.FolderPath, op.Name!, op.NewName, op.DataType, op.LogicalAddress, op.ExternalAccessible, op.ExternalVisible, op.ExternalWritable, op.IsSafety, projectPath, op.TiaVersion),
            "delete_tag" => workerClient.DeleteTagAsync(op.PlcName, op.TableName!, op.FolderPath, op.Name!, projectPath, op.TiaVersion),
            "create_tag_table" => workerClient.CreateTagTableAsync(op.PlcName, op.TableName!, op.FolderPath, projectPath, op.TiaVersion),
            "delete_tag_table" => workerClient.DeleteTagTableAsync(op.PlcName, op.TableName!, op.FolderPath, projectPath, op.TiaVersion),
            "create_user_constant" => workerClient.CreateUserConstantAsync(op.PlcName, op.TableName!, op.FolderPath, op.Name!, op.DataType!, op.Value!, projectPath, op.TiaVersion),
            "update_user_constant" => workerClient.UpdateUserConstantAsync(op.PlcName, op.TableName!, op.FolderPath, op.Name!, op.DataType, op.Value, projectPath, op.TiaVersion),
            "delete_user_constant" => workerClient.DeleteUserConstantAsync(op.PlcName, op.TableName!, op.FolderPath, op.Name!, projectPath, op.TiaVersion),
            "create_block_group" => workerClient.CreateBlockGroupAsync(op.GroupPath!, confirm: true, projectPath, op.TiaVersion),
            "delete_block_group" => workerClient.DeleteBlockGroupAsync(op.GroupPath!, confirm: true, projectPath, op.TiaVersion),
            _ => Task.FromResult("Error: unknown operation '" + op.Operation + "'"),
        };

    private static string Error(string tool, string message, object? errors = null, string? operationId = null)
        => JsonSerializer.Serialize(new
        {
            tool,
            error = message,
            operationId,
            errors,
        }, JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
