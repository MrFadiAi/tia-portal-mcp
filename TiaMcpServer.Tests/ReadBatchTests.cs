using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Worker;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// Unit tests for the pure read_batch building blocks (<see cref="ReadBatchCatalog"/>
/// whitelist/validation, <see cref="ReadBatchBudget"/> payload caps, <see cref="ReadBatchResponse"/>
/// shape). The orchestration itself (OpennessWorkerClient.ReadBatchAsync) lives behind worker I/O
/// and is covered by the tool-level contract these classes pin down.
/// </summary>
public class ReadBatchTests
{
    private static ReadBatchOperation Op(string id, string operation, string? blockPath = null,
        string? query = null, string? tag = null)
        => new() { OperationId = id, Operation = operation, BlockPath = blockPath, Query = query, Tag = tag };

    // ------------------------------------------------------------------ catalog

    [Fact]
    public void Valid_Batch_Passes_Validation()
    {
        var result = ReadBatchCatalog.Validate(new List<ReadBatchOperation>
        {
            Op("a", "list_blocks"),
            Op("b", "get_block_content", blockPath: "PLC_1/FC100"),
            Op("c", "find_tags", query: "MOTOR"),
            Op("d", "tag_usage", tag: "PLUKSCHIJF RUNNING"),
        });

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Empty_Batch_Is_Rejected()
    {
        var result = ReadBatchCatalog.Validate(new List<ReadBatchOperation>());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Error.Contains("empty"));
    }

    [Fact]
    public void Unknown_Operation_Is_Rejected_With_The_Operation_Name_And_Whitelist()
    {
        var result = ReadBatchCatalog.Validate(new List<ReadBatchOperation> { Op("a", "drop_dead") });

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Contains("drop_dead", error.Error);
        Assert.Contains("list_blocks", error.Error); // the whitelist is named so the caller can fix it
        Assert.Equal(0, error.Index);
    }

    [Fact]
    public void Missing_Required_Field_Error_Names_Field_And_Operation()
    {
        // get_block_content without blockPath
        var result = ReadBatchCatalog.Validate(new List<ReadBatchOperation> { Op("a", "get_block_content") });

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Contains("get_block_content", error.Error);
        Assert.Contains("blockPath", error.Error);
    }

    [Fact]
    public void Missing_Query_On_Search_Code_Is_Rejected()
    {
        var result = ReadBatchCatalog.Validate(new List<ReadBatchOperation> { Op("a", "search_code") });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Error.Contains("query"));
    }

    [Fact]
    public void Batch_Over_MaxBatchSize_Is_Rejected()
    {
        var items = Enumerable.Range(0, ReadBatchCatalog.MaxBatchSize + 1)
            .Select(i => Op("op" + i, "list_blocks"))
            .ToList();

        var result = ReadBatchCatalog.Validate(items);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Error.Contains("26") && e.Error.Contains(ReadBatchCatalog.MaxBatchSize.ToString()));
    }

    [Fact]
    public void Duplicate_OperationId_Is_Rejected_On_The_Second_Occurrence()
    {
        var result = ReadBatchCatalog.Validate(new List<ReadBatchOperation>
        {
            Op("dup", "list_blocks"),
            Op("dup", "list_tag_tables"),
        });

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Contains("duplicate", error.Error);
        Assert.Equal("dup", error.OperationId);
        Assert.Equal(1, error.Index); // blames the second occurrence, not the first
    }

    [Fact]
    public void Known_Operation_Names_Are_Case_Insensitive()
    {
        var result = ReadBatchCatalog.Validate(new List<ReadBatchOperation> { Op("a", "LIST_BLOCKS") });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Missing_OperationId_Is_Rejected()
    {
        var result = ReadBatchCatalog.Validate(new List<ReadBatchOperation> { Op("", "list_blocks") });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Error.Contains("operationId"));
    }

    // ------------------------------------------------------------------ budget

    [Fact]
    public void Item_Cap_Truncates_With_Marker()
    {
        var big = new string('x', ReadBatchBudget.MaxItemChars + 5_000);

        var capped = ReadBatchBudget.ApplyItemCap(big);

        Assert.StartsWith(new string('x', ReadBatchBudget.MaxItemChars), capped);
        Assert.EndsWith($"...[truncated, {big.Length} chars total]", capped);
        // cap + newline + marker: still far smaller than the original
        Assert.True(capped.Length < big.Length);
    }

    [Fact]
    public void Item_Cap_Keeps_Short_Results_Unchanged()
    {
        var small = "[{\"name\":\"FC100\"}]";

        Assert.Equal(small, ReadBatchBudget.ApplyItemCap(small));
    }

    [Fact]
    public void Batch_Cap_Omits_Later_Items_With_The_Hint()
    {
        var items = new List<ReadBatchOperationResult>
        {
            new() { OperationId = "a", Operation = "get_block_content", Status = "succeeded", Result = new string('x', 100_000) },
            new() { OperationId = "b", Operation = "get_block_content", Status = "succeeded", Result = new string('y', 100_000) },
            new() { OperationId = "c", Operation = "list_blocks", Status = "succeeded", Result = "tiny" },
        };

        ReadBatchBudget.ApplyBatchCap(items);

        Assert.Equal("succeeded", items[0].Status); // earlier items keep full results
        Assert.Equal(100_000, items[0].Result.Length);
        Assert.Equal("omitted", items[1].Status);
        Assert.Contains("budget exhausted", items[1].Result);
        Assert.Contains("re-run", items[1].Result);
        Assert.Equal("omitted", items[2].Status); // everything after the first overflow too
    }

    [Fact]
    public void Failed_Item_Is_Never_Omitted_By_Batch_Cap()
    {
        // 'a' fills the budget to 29 chars of the cap; 'b' is a failure whose 29-char error
        // would overflow if failed items were subject to omission — it must survive as failed.
        // 'c' then overflows for real and is omitted.
        var items = new List<ReadBatchOperationResult>
        {
            new() { OperationId = "a", Operation = "get_block_content", Status = "succeeded", Result = new string('x', 149_971) },
            new() { OperationId = "b", Operation = "tag_usage", Status = "failed", Result = "Error: tag not found anywhere" },
            new() { OperationId = "c", Operation = "list_blocks", Status = "succeeded", Result = "tiny" },
        };

        ReadBatchBudget.ApplyBatchCap(items);

        Assert.Equal("succeeded", items[0].Status);
        Assert.Equal("failed", items[1].Status);
        Assert.Equal("Error: tag not found anywhere", items[1].Result);
        Assert.Equal("omitted", items[2].Status);
    }

    // ------------------------------------------------------------------ response shape

    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void Response_Renders_Counts_And_Operations()
    {
        var operations = new List<ReadBatchOperationResult>
        {
            new() { OperationId = "a", Operation = "get_block_content", Status = "succeeded", Ms = 123, Result = "code" },
            new() { OperationId = "b", Operation = "tag_usage", Status = "failed", Ms = 5, Result = "Error: boom" },
            new() { OperationId = "c", Operation = "list_blocks", Status = "omitted", Ms = 0, Result = "omitted by budget" },
        };

        var json = JsonSerializer.Serialize(ReadBatchResponse.For(operations), CamelCase);

        Assert.Contains("\"tool\":\"read_batch\"", json);
        Assert.Contains("\"operationCount\":3", json);
        Assert.Contains("\"succeeded\":1", json);
        Assert.Contains("\"failed\":1", json);
        Assert.Contains("\"omitted\":1", json);
        Assert.Contains("\"operationId\":\"a\"", json);
        Assert.Contains("\"operation\":\"get_block_content\"", json);
        Assert.Contains("\"status\":\"succeeded\"", json);
        Assert.Contains("\"ms\":123", json);
        Assert.Contains("\"result\":\"code\"", json);
    }
}
