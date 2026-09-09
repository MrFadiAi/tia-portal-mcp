using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TiaMcpServer.Safety;

namespace TiaMcpServer.Worker;

/// <summary>One write operation inside a preview_write_batch / apply_write_batch call. All
/// fields optional except operationId + operation; the catalog enforces per-operation
/// required fields BEFORE any worker call is made.</summary>
public sealed class WriteBatchOperation
{
    [JsonPropertyName("operationId")] public string OperationId { get; set; } = "";
    [JsonPropertyName("operation")] public string Operation { get; set; } = "";
    [JsonPropertyName("plcName")] public string? PlcName { get; set; }
    [JsonPropertyName("tableName")] public string? TableName { get; set; }
    [JsonPropertyName("folderPath")] public string? FolderPath { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("newName")] public string? NewName { get; set; }
    [JsonPropertyName("dataType")] public string? DataType { get; set; }
    [JsonPropertyName("logicalAddress")] public string? LogicalAddress { get; set; }
    [JsonPropertyName("value")] public string? Value { get; set; }
    [JsonPropertyName("externalAccessible")] public bool? ExternalAccessible { get; set; }
    [JsonPropertyName("externalVisible")] public bool? ExternalVisible { get; set; }
    [JsonPropertyName("externalWritable")] public bool? ExternalWritable { get; set; }
    [JsonPropertyName("isSafety")] public bool? IsSafety { get; set; }
    [JsonPropertyName("groupPath")] public string? GroupPath { get; set; }
    [JsonPropertyName("projectPath")] public string? ProjectPath { get; set; }
    [JsonPropertyName("tiaVersion")] public int? TiaVersion { get; set; }
}

/// <summary>
/// Whitelist + validation for write-batch operations. Pure (Siemens-free) so it is link-compiled
/// into the test project, and runs BEFORE any worker call — an invalid batch costs nothing and,
/// being a WRITE batch, must never reach the worker. Deliberately EXCLUDED from the whitelist
/// (each has its own heavier preview/confirm ceremony that a grouped token must not bypass):
/// update_block_logic, update_type_content, create/update/delete_subnet, start_plc, stop_plc.
/// Writes are heavier than reads, so the batch is smaller (10 vs read_batch's 25): smaller
/// batch = smaller blast radius when a mid-batch failure stops the run.
/// </summary>
public static class WriteBatchCatalog
{
    public const int MaxBatchSize = 10;

    /// <summary>operation name → fields that must be non-empty on the item.</summary>
    private static readonly Dictionary<string, string[]> RequiredFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["create_tag"] = new[] { "tableName", "name", "dataType" },
            ["update_tag"] = new[] { "tableName", "name" },
            ["delete_tag"] = new[] { "tableName", "name" },
            ["create_tag_table"] = new[] { "tableName" },
            ["delete_tag_table"] = new[] { "tableName" },
            ["create_user_constant"] = new[] { "tableName", "name", "dataType", "value" },
            ["update_user_constant"] = new[] { "tableName", "name" },
            ["delete_user_constant"] = new[] { "tableName", "name" },
            ["create_block_group"] = new[] { "groupPath" },
            ["delete_block_group"] = new[] { "groupPath" },
        };

    public static string WhitelistSummary()
        => string.Join(", ", RequiredFields.Keys.OrderBy(k => k, StringComparer.Ordinal));

    /// <summary>The tag-table-family operations (their per-op preview binds the list_tag_tables
    /// payload as current state, so the batch composes that same read per item).</summary>
    public static bool IsTagFamilyOperation(string operation)
        => !IsBlockGroupOperation(operation);

    /// <summary>Block-group operations (their per-op preview is the worker dry run: parent/
    /// name-exists check for create, blast-radius counts for delete).</summary>
    public static bool IsBlockGroupOperation(string operation)
        => string.Equals(operation, "create_block_group", StringComparison.OrdinalIgnoreCase)
           || string.Equals(operation, "delete_block_group", StringComparison.OrdinalIgnoreCase);

    public static WriteBatchValidationResult Validate(IReadOnlyList<WriteBatchOperation>? items)
    {
        var result = new WriteBatchValidationResult();

        if (items is null || items.Count == 0)
        {
            result.Errors.Add(new WriteBatchValidationError { Error = "operations list is empty" });
            return result;
        }

        if (items.Count > MaxBatchSize)
        {
            result.Errors.Add(new WriteBatchValidationError
            {
                Error = $"batch exceeds MaxBatchSize ({items.Count} items, max {MaxBatchSize})",
            });
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var op = item.Operation ?? "";

            if (string.IsNullOrWhiteSpace(item.OperationId))
            {
                result.Errors.Add(new WriteBatchValidationError
                {
                    Index = i, Operation = op, Error = "missing operationId",
                });
            }
            else if (!seenIds.Add(item.OperationId))
            {
                result.Errors.Add(new WriteBatchValidationError
                {
                    Index = i, OperationId = item.OperationId, Operation = op,
                    Error = $"duplicate operationId '{item.OperationId}'",
                });
            }

            if (!RequiredFields.TryGetValue(op, out var required))
            {
                result.Errors.Add(new WriteBatchValidationError
                {
                    Index = i, OperationId = item.OperationId, Operation = op,
                    Error = $"unknown operation '{op}' (allowed: {WhitelistSummary()})",
                });
                continue;
            }

            foreach (var field in required)
            {
                if (string.IsNullOrWhiteSpace(FieldValue(item, field)))
                {
                    result.Errors.Add(new WriteBatchValidationError
                    {
                        Index = i, OperationId = item.OperationId, Operation = op,
                        Error = $"{op}: missing required field '{field}'",
                    });
                }
            }
        }

        if (result.IsValid)
        {
            var projectError = CheckSingleProject(items);
            if (projectError is not null)
            {
                result.Errors.Add(new WriteBatchValidationError { Error = projectError });
            }
        }

        return result;
    }

    /// <summary>All operations must target the same project: every non-null projectPath must be
    /// identical after normalization (a null projectPath means "the active project", which is
    /// compatible with any explicit path that resolves to it). Returns the resolved batch-wide
    /// project path, or an error when the item paths conflict.</summary>
    public static string? ResolveProjectPath(IReadOnlyList<WriteBatchOperation> items, out string? error)
    {
        var distinct = items
            .Where(i => !string.IsNullOrWhiteSpace(i.ProjectPath))
            .Select(i => WriteSafetyService.NormalizeProjectPath(i.ProjectPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count > 1)
        {
            error = "all operations in a write batch must target the same project — got: " +
                    string.Join(", ", distinct.Select(p => $"'{p}'"));
            return null;
        }

        error = null;
        return distinct.Count == 1 ? distinct[0] : null;
    }

    private static string? CheckSingleProject(IReadOnlyList<WriteBatchOperation> items)
        => ResolveProjectPath(items, out var error) == null && error is not null ? error : null;

    private static string? FieldValue(WriteBatchOperation item, string field)
        => field switch
        {
            "tableName" => item.TableName,
            "name" => item.Name,
            "dataType" => item.DataType,
            "value" => item.Value,
            "groupPath" => item.GroupPath,
            _ => null,
        };
}

/// <summary>
/// Pure helpers building what the ONE batch-level safety token binds: the ordered target list
/// and the composed current-state fingerprint. Kept Siemens-free so token binding is unit-testable.
/// </summary>
public static class WriteBatchSnapshot
{
    /// <summary>Ordered (operationId, operation, description) list — this exact list, in this
    /// exact order, is what apply_write_batch must present again for the token to validate.</summary>
    public static IReadOnlyList<object> BuildTargets(IReadOnlyList<WriteBatchOperation> operations)
        => operations
            .Select(op => (object)new
            {
                operationId = op.OperationId,
                operation = op.Operation,
                description = DescribeOperation(op),
            })
            .ToArray();

    public static string DescribeOperation(WriteBatchOperation op) => (op.Operation ?? "").ToLowerInvariant() switch
    {
        "create_tag_table" => $"Create PLC tag table '{op.TableName}'.",
        "delete_tag_table" => $"Delete PLC tag table '{op.TableName}'.",
        "create_tag" => $"Create PLC tag '{op.Name}' in table '{op.TableName}'.",
        "update_tag" => $"Update PLC tag '{op.Name}' in table '{op.TableName}'.",
        "delete_tag" => $"Delete PLC tag '{op.Name}' from table '{op.TableName}'.",
        "create_user_constant" => $"Create PLC user constant '{op.Name}' in table '{op.TableName}'.",
        "update_user_constant" => $"Update PLC user constant '{op.Name}' in table '{op.TableName}'.",
        "delete_user_constant" => $"Delete PLC user constant '{op.Name}' from table '{op.TableName}'.",
        "create_block_group" => $"Create PLC block group '{op.GroupPath}'.",
        "delete_block_group" => $"Delete PLC block group '{op.GroupPath}'.",
        _ => $"{op.Operation}.",
    };

    /// <summary>
    /// Compose the batch-wide current-state fingerprint: per-op state hash (the exact payload each
    /// op's own preview binds — list_tag_tables for tag ops, the worker dry run for block groups),
    /// joined IN ORDER and hashed again. Any single op's state change, or any reordering, changes
    /// the fingerprint, so apply rejects until the project is exactly as previewed.
    /// </summary>
    public static string ComposeStateFingerprint(IReadOnlyList<(string OperationId, string State)> perOpStates)
    {
        var combined = string.Join(
            ";",
            perOpStates.Select(p => $"{p.OperationId}:{WriteSafetyService.HashText(p.State)}"));
        return WriteSafetyService.HashText(combined);
    }
}

public sealed class WriteBatchValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<WriteBatchValidationError> Errors { get; } = new();
}

public sealed class WriteBatchValidationError
{
    public int? Index { get; set; }
    public string? OperationId { get; set; }
    public string Operation { get; set; } = "";
    public string Error { get; set; } = "";
}
