using System;
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

    // ------------------------------------------------------------ batch ladder
    // (ported failure-preserving degradation: success payloads drop before failure details,
    //  failure details drop before warnings, and a failed item is never dropped outright)

    [Fact]
    public void Within_Budget_List_Is_Untouched()
    {
        var items = new List<ReadBatchOperationResult>
        {
            new() { OperationId = "a", Operation = "list_blocks", Status = "succeeded", Result = new string('x', 10_000) },
            new() { OperationId = "b", Operation = "tag_usage", Status = "failed", Result = "Error: boom" },
        };

        ReadBatchBudget.ApplyBatchLadder(items);

        Assert.Equal(new string('x', 10_000), items[0].Result);
        Assert.Equal("Error: boom", items[1].Result);
    }

    [Fact]
    public void Ladder_Drops_Success_Payloads_Before_Failure_Details()
    {
        var items = new List<ReadBatchOperationResult>();
        for (var i = 0; i < 5; i++)
        {
            items.Add(new ReadBatchOperationResult
            {
                OperationId = "s" + i, Operation = "get_block_content", Status = "succeeded",
                Result = new string('x', 30_000),
            });
        }
        items.Add(new ReadBatchOperationResult
        {
            OperationId = "f", Operation = "tag_usage", Status = "failed",
            Result = "Error: tag not found anywhere",
        });

        ReadBatchBudget.ApplyBatchLadder(items);

        // one success marker was enough room — the failure keeps EVERYTHING
        Assert.Equal(ReadBatchBudget.OmittedSuccessMarker, items[0].Result);
        Assert.Equal("succeeded", items[0].Status); // status survives; only the payload is gone
        Assert.Equal(30_000, items[1].Result.Length);
        Assert.Equal(30_000, items[4].Result.Length);
        Assert.Equal("Error: tag not found anywhere", items[5].Result);
        Assert.Equal("failed", items[5].Status);
        Assert.True(ReadBatchBudget.TotalChars(items) <= ReadBatchBudget.MaxBatchChars);
    }

    [Fact]
    public void Failure_Detail_Is_Never_Truncated_While_Any_Success_Payload_Was_Dropped()
    {
        // the failure detail alone is huge (145k > rescue cap) but dropping the one success
        // payload makes the batch fit — so the detail must NOT be touched
        var items = new List<ReadBatchOperationResult>
        {
            new() { OperationId = "a", Operation = "get_block_content", Status = "succeeded", Result = new string('x', 20_000) },
            new() { OperationId = "b", Operation = "tag_usage", Status = "failed", Result = "Error: " + new string('e', 144_993) },
        };

        ReadBatchBudget.ApplyBatchLadder(items);

        Assert.Equal(ReadBatchBudget.OmittedSuccessMarker, items[0].Result);
        Assert.Equal(145_000, items[1].Result.Length); // full detail, not the 2k rescue head
        Assert.StartsWith("Error: ", items[1].Result);
    }

    [Fact]
    public void Overflowed_Item_Failure_Survives()
    {
        // the LAST item is the failure that pushes the batch over budget (7×21.5k + 29 > 150k)
        var items = new List<ReadBatchOperationResult>();
        for (var i = 0; i < 7; i++)
        {
            items.Add(new ReadBatchOperationResult
            {
                OperationId = "s" + i, Operation = "get_block_content", Status = "succeeded",
                Result = new string('x', 21_500),
            });
        }
        items.Add(new ReadBatchOperationResult
        {
            OperationId = "f", Operation = "tag_usage", Status = "failed",
            Result = "Error: tag not found anywhere",
        });

        ReadBatchBudget.ApplyBatchLadder(items);

        Assert.Equal("Error: tag not found anywhere", items[7].Result);
        Assert.Equal("failed", items[7].Status);
        // the earliest success made room; everything else is untouched
        Assert.Equal(ReadBatchBudget.OmittedSuccessMarker, items[0].Result);
        Assert.Equal(21_500, items[1].Result.Length);
        Assert.Equal(21_500, items[6].Result.Length);
    }

    [Fact]
    public void Earliest_Success_Payload_Drops_First()
    {
        var items = new List<ReadBatchOperationResult>
        {
            new() { OperationId = "a", Operation = "get_block_content", Status = "succeeded", Result = new string('x', 100_000) },
            new() { OperationId = "b", Operation = "get_block_content", Status = "succeeded", Result = new string('y', 100_000) },
            new() { OperationId = "c", Operation = "list_blocks", Status = "succeeded", Result = "tiny" },
        };

        ReadBatchBudget.ApplyBatchLadder(items);

        Assert.Equal(ReadBatchBudget.OmittedSuccessMarker, items[0].Result);
        Assert.Equal("succeeded", items[0].Status);
        Assert.Equal(100_000, items[1].Result.Length); // later results keep their payloads
        Assert.Equal("tiny", items[2].Result);
    }

    [Fact]
    public void Failed_Item_Survives_With_Full_Detail()
    {
        // 'a' fills the budget to 29 chars of the cap; 'b' is a failure whose 29-char error
        // would overflow — IT must survive intact, and the success payload gives way.
        var items = new List<ReadBatchOperationResult>
        {
            new() { OperationId = "a", Operation = "get_block_content", Status = "succeeded", Result = new string('x', 149_971) },
            new() { OperationId = "b", Operation = "tag_usage", Status = "failed", Result = "Error: tag not found anywhere" },
            new() { OperationId = "c", Operation = "list_blocks", Status = "succeeded", Result = "tiny" },
        };

        ReadBatchBudget.ApplyBatchLadder(items);

        Assert.Equal(ReadBatchBudget.OmittedSuccessMarker, items[0].Result);
        Assert.Equal("failed", items[1].Status);
        Assert.Equal("Error: tag not found anywhere", items[1].Result);
        Assert.Equal("tiny", items[2].Result);
    }

    [Fact]
    public void Failure_Detail_Truncation_Keeps_The_Head()
    {
        // no successes to drop, so over-budget failures truncate to the rescue head — the head
        // is where "Error:" and the write-batch stop-warning live, so it must survive
        var warning = "WARNING: this operation and any earlier operation may have changed TIA state.";
        var items = new List<ReadBatchOperationResult>();
        for (var i = 0; i < 10; i++)
        {
            items.Add(new ReadBatchOperationResult
            {
                OperationId = "f" + i, Operation = "tag_usage", Status = "failed",
                Result = (i == 0 ? warning + " " : "Error: ") + new string('e', 20_000),
            });
        }

        ReadBatchBudget.ApplyBatchLadder(items);

        Assert.True(items[0].Result.StartsWith(warning, StringComparison.Ordinal));
        Assert.Contains("failure detail truncated for budget", items[0].Result);
        Assert.True(items[0].Result.Length <= ReadBatchBudget.FailureDetailRescueChars + 100);
        Assert.Equal("failed", items[0].Status);
        // later items were never touched once the batch fit
        Assert.Equal(20_000 + 7, items[9].Result.Length);
        Assert.True(ReadBatchBudget.TotalChars(items) <= ReadBatchBudget.MaxBatchChars);
    }

    [Fact]
    public void Warnings_Are_Truncated_After_Failure_Details()
    {
        // details are already under the rescue cap (so step 2 is a no-op) and the batch is over
        // budget purely because of warnings — warnings must collapse, details must not
        var items = new List<ReadBatchOperationResult>();
        for (var i = 0; i < 50; i++)
        {
            items.Add(new ReadBatchOperationResult
            {
                OperationId = "f" + i, Operation = "tag_usage", Status = "failed",
                Result = "Error: " + new string('e', 1_493),
                Warnings = Enumerable.Range(0, 3).Select(w => new string('w', 700)).ToList(),
            });
        }

        ReadBatchBudget.ApplyBatchLadder(items);

        Assert.All(items, item => Assert.Equal(1_500, item.Result.Length)); // every detail intact
        var collapsed = items.Count(i => i.Warnings is { Count: 1 }
                                         && i.Warnings[0] == ReadBatchBudget.WarningsTruncationMarker);
        Assert.True(collapsed > 0 && collapsed < 50, $"expected partial collapse, got {collapsed}");
        Assert.True(ReadBatchBudget.TotalChars(items) <= ReadBatchBudget.MaxBatchChars);
    }

    [Fact]
    public void Warnings_Count_Toward_The_Budget()
    {
        // without counting warnings this batch would fit (5 + 75_000 < 150k) and nothing would
        // degrade — the degradation proves warnings are part of the accounting
        var items = new List<ReadBatchOperationResult>
        {
            new()
            {
                OperationId = "a", Operation = "list_blocks", Status = "succeeded", Result = "tiny",
                Warnings = Enumerable.Range(0, 80).Select(w => new string('w', 1_000)).ToList(),
            },
            new() { OperationId = "b", Operation = "get_block_content", Status = "succeeded", Result = new string('x', 75_000) },
        };

        ReadBatchBudget.ApplyBatchLadder(items);

        Assert.Equal(ReadBatchBudget.OmittedSuccessMarker, items[0].Result);
        Assert.Null(items[0].Warnings);
        Assert.Equal(75_000, items[1].Result.Length);
    }

    [Fact]
    public void Terminal_Clamp_Never_Throws_And_Never_Drops_Failures()
    {
        // pathological: 100 failures whose rescue-sized details + warnings cannot fit — the
        // ladder must still terminate without throwing and keep every item's failed status
        var items = new List<ReadBatchOperationResult>();
        for (var i = 0; i < 100; i++)
        {
            items.Add(new ReadBatchOperationResult
            {
                OperationId = "f" + i, Operation = "tag_usage", Status = "failed",
                Result = "Error: " + new string('e', 19_993),
                Warnings = Enumerable.Range(0, 3).Select(w => new string('w', 5_000)).ToList(),
            });
        }

        ReadBatchBudget.ApplyBatchLadder(items);

        Assert.All(items, item => Assert.Equal("failed", item.Status));
        Assert.All(items, item => Assert.StartsWith("Error: ", item.Result));
        Assert.All(items, item => Assert.True(item.Result.Length > 0));
        Assert.True(ReadBatchBudget.TotalChars(items) <= ReadBatchBudget.MaxBatchChars);
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
