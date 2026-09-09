using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Safety;
using TiaMcpServer.Worker;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// The ONE batch-level safety token must bind the EXACT ordered operation list and the composed
/// current-state fingerprint, be single-use, and expire. These tests drive the real
/// WriteSafetyService exactly as WriteBatchTools does (CreatePreview -> ValidateAndConsume) with a
/// fixed clock, proving: tamper/reorder/add/remove rejection, state-change rejection, exact-match
/// acceptance, and single-use consumption.
/// </summary>
public class WriteBatchTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const string ApplyTool = "apply_write_batch";

    private static WriteBatchOperation TagOp(string id, string? name = null)
        => new() { OperationId = id, Operation = "create_tag", TableName = "IO", Name = name ?? "n_" + id, DataType = "Bool" };

    private static (WriteSafetyService Safety, string Token) Mint(
        List<WriteBatchOperation> operations, IReadOnlyList<(string, string)> states)
    {
        var safety = new WriteSafetyService(() => Now, TimeSpan.FromMinutes(10));
        var fingerprint = WriteBatchSnapshot.ComposeStateFingerprint(states);
        var previewJson = safety.CreatePreview(
            ApplyTool,
            projectPath: null,
            target: WriteBatchSnapshot.BuildTargets(operations),
            summary: "test summary",
            requestedInput: operations,
            currentState: fingerprint);
        var token = JsonDocument.Parse(previewJson).RootElement.GetProperty("safetyToken").GetString()!;
        return (safety, token);
    }

    private static WriteSafetyValidationResult Validate(
        WriteSafetyService safety, string token,
        List<WriteBatchOperation> operations, IReadOnlyList<(string, string)> states)
        => safety.ValidateAndConsume(
            token,
            ApplyTool,
            projectPath: null,
            target: WriteBatchSnapshot.BuildTargets(operations),
            requestedInput: operations,
            currentState: WriteBatchSnapshot.ComposeStateFingerprint(states));

    private static readonly List<WriteBatchOperation> TwoOps = new() { TagOp("a"), TagOp("b") };
    private static readonly List<(string, string)> TwoStates = new() { ("a", "tables-v1"), ("b", "tables-v1") };

    [Fact]
    public void Exact_Same_List_And_State_Is_Accepted()
    {
        var (safety, token) = Mint(TwoOps, TwoStates);
        var sameList = new List<WriteBatchOperation> { TagOp("a"), TagOp("b") };

        var result = Validate(safety, token, sameList, TwoStates);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Reordered_List_Is_Rejected()
    {
        var (safety, token) = Mint(TwoOps, TwoStates);
        var reordered = new List<WriteBatchOperation> { TagOp("b", "n_b"), TagOp("a", "n_a") };

        var result = Validate(safety, token, reordered, TwoStates);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Added_Operation_Is_Rejected()
    {
        var (safety, token) = Mint(TwoOps, TwoStates);
        var grown = new List<WriteBatchOperation> { TagOp("a"), TagOp("b"), TagOp("c") };

        var result = Validate(safety, token, grown, TwoStates);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Removed_Operation_Is_Rejected()
    {
        var (safety, token) = Mint(TwoOps, TwoStates);
        var shrunk = new List<WriteBatchOperation> { TagOp("a") };

        var result = Validate(safety, token, shrunk, new[] { ("a", "tables-v1") });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Changed_Field_Value_Is_Rejected()
    {
        // same shape, same order — but one op's dataType differs
        var (safety, token) = Mint(TwoOps, TwoStates);
        var mutated = new List<WriteBatchOperation>
        {
            TagOp("a"),
            new() { OperationId = "b", Operation = "create_tag", TableName = "IO", Name = "n_b", DataType = "Int" },
        };

        var result = Validate(safety, token, mutated, TwoStates);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Changed_Project_State_Is_Rejected()
    {
        var (safety, token) = Mint(TwoOps, TwoStates);
        var changedState = new List<(string, string)> { ("a", "tables-v1"), ("b", "tables-v2") };

        var result = Validate(safety, token, TwoOps, changedState);

        Assert.False(result.IsValid);
        Assert.Contains("current state", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Token_Is_Single_Use()
    {
        var (safety, token) = Mint(TwoOps, TwoStates);

        var first = Validate(safety, token, TwoOps, TwoStates);
        var second = Validate(safety, token, TwoOps, TwoStates);

        Assert.True(first.IsValid);
        Assert.False(second.IsValid);
        Assert.Contains("consumed", second.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Token_Expires_After_Its_Lifetime()
    {
        var time = Now;
        var safety = new WriteSafetyService(() => time, TimeSpan.FromMinutes(10));
        var fingerprint = WriteBatchSnapshot.ComposeStateFingerprint(TwoStates);
        var previewJson = safety.CreatePreview(
            ApplyTool, null, WriteBatchSnapshot.BuildTargets(TwoOps), "s", TwoOps, fingerprint);
        var token = JsonDocument.Parse(previewJson).RootElement.GetProperty("safetyToken").GetString()!;

        time = Now.AddMinutes(11);
        var result = safety.ValidateAndConsume(
            token, ApplyTool, null, WriteBatchSnapshot.BuildTargets(TwoOps), TwoOps, fingerprint);

        Assert.False(result.IsValid);
        Assert.Contains("expired", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fingerprint_Composition_Is_Order_And_Content_Sensitive()
    {
        var ab = WriteBatchSnapshot.ComposeStateFingerprint(new[] { ("a", "s1"), ("b", "s2") });
        var ba = WriteBatchSnapshot.ComposeStateFingerprint(new[] { ("b", "s2"), ("a", "s1") });
        var changed = WriteBatchSnapshot.ComposeStateFingerprint(new[] { ("a", "s1"), ("b", "CHANGED") });
        var same = WriteBatchSnapshot.ComposeStateFingerprint(new[] { ("a", "s1"), ("b", "s2") });

        Assert.NotEqual(ab, ba);       // order matters
        Assert.NotEqual(ab, changed);  // content matters
        Assert.Equal(ab, same);        // deterministic
    }

    [Fact]
    public void Targets_List_The_Operations_In_Order_With_Descriptions()
    {
        var targets = WriteBatchSnapshot.BuildTargets(new List<WriteBatchOperation>
        {
            TagOp("a"),
            new() { OperationId = "g", Operation = "delete_block_group", GroupPath = "PLC_1/Blocks/G1" },
        });

        // Parse the stable JSON the token actually binds (the default JS encoder escapes quotes,
        // so substring-matching the raw JSON would be brittle) and assert ids + descriptions in order.
        using var doc = JsonDocument.Parse(WriteSafetyService.ToStableJson(targets));
        var entries = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal("a", entries[0].GetProperty("operationId").GetString());
        Assert.Equal("Create PLC tag 'n_a' in table 'IO'.", entries[0].GetProperty("description").GetString());
        Assert.Equal("g", entries[1].GetProperty("operationId").GetString());
        Assert.Equal("Delete PLC block group 'PLC_1/Blocks/G1'.", entries[1].GetProperty("description").GetString());
    }
}
