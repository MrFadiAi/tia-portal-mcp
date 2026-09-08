using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace TiaMcpServer.Worker;

/// <summary>One read operation inside a read_batch call. All fields optional except
/// operationId + operation (validated by <see cref="ReadBatchCatalog"/>).</summary>
public sealed class ReadBatchOperation
{
    [JsonPropertyName("operationId")] public string OperationId { get; set; } = "";
    [JsonPropertyName("operation")] public string Operation { get; set; } = "";
    [JsonPropertyName("plcName")] public string? PlcName { get; set; }
    [JsonPropertyName("blockPath")] public string? BlockPath { get; set; }
    [JsonPropertyName("query")] public string? Query { get; set; }
    [JsonPropertyName("tag")] public string? Tag { get; set; }
    [JsonPropertyName("contextLines")] public int? ContextLines { get; set; }
    [JsonPropertyName("projectPath")] public string? ProjectPath { get; set; }
    [JsonPropertyName("tiaVersion")] public int? TiaVersion { get; set; }
}

/// <summary>
/// Whitelist + validation for read_batch operations. Pure (Siemens-free) so it is link-compiled
/// into the test project, and runs BEFORE any worker call — an invalid batch costs nothing.
/// The whitelist maps each operation name to the item fields it requires.
/// </summary>
public static class ReadBatchCatalog
{
    public const int MaxBatchSize = 25;

    /// <summary>operation name → fields that must be non-empty on the item.</summary>
    private static readonly Dictionary<string, string[]> RequiredFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["get_block_content"] = new[] { "blockPath" },
            ["read_block_interface"] = new[] { "blockPath" },
            ["list_blocks"] = Array.Empty<string>(),
            ["list_tag_tables"] = Array.Empty<string>(),
            ["find_tags"] = new[] { "query" },
            ["search_code"] = new[] { "query" },
            ["tag_usage"] = new[] { "tag" },
        };

    public static string WhitelistSummary()
        => string.Join(", ", RequiredFields.Keys.OrderBy(k => k, StringComparer.Ordinal));

    public static ReadBatchValidationResult Validate(IReadOnlyList<ReadBatchOperation>? items)
    {
        var result = new ReadBatchValidationResult();

        if (items is null || items.Count == 0)
        {
            result.Errors.Add(new ReadBatchValidationError { Error = "operations list is empty" });
            return result;
        }

        if (items.Count > MaxBatchSize)
        {
            result.Errors.Add(new ReadBatchValidationError
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
                result.Errors.Add(new ReadBatchValidationError
                {
                    Index = i, Operation = op, Error = "missing operationId",
                });
            }
            else if (!seenIds.Add(item.OperationId))
            {
                result.Errors.Add(new ReadBatchValidationError
                {
                    Index = i, OperationId = item.OperationId, Operation = op,
                    Error = $"duplicate operationId '{item.OperationId}'",
                });
            }

            if (!RequiredFields.TryGetValue(op, out var required))
            {
                result.Errors.Add(new ReadBatchValidationError
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
                    result.Errors.Add(new ReadBatchValidationError
                    {
                        Index = i, OperationId = item.OperationId, Operation = op,
                        Error = $"{op}: missing required field '{field}'",
                    });
                }
            }
        }

        return result;
    }

    private static string? FieldValue(ReadBatchOperation item, string field)
        => field switch
        {
            "blockPath" => item.BlockPath,
            "query" => item.Query,
            "tag" => item.Tag,
            _ => null,
        };
}

public sealed class ReadBatchValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<ReadBatchValidationError> Errors { get; } = new();
}

public sealed class ReadBatchValidationError
{
    public int? Index { get; set; }
    public string? OperationId { get; set; }
    public string Operation { get; set; } = "";
    public string Error { get; set; } = "";
}
