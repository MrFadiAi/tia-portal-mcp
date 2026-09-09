using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Worker;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// Unit tests for the pure write-batch catalog: whitelist + validation run BEFORE any worker
/// call, so an invalid WRITE batch costs nothing and can never reach TIA. Mirrors the read_batch
/// catalog tests, plus the single-project rule (all ops must target the same project).
/// </summary>
public class WriteBatchCatalogTests
{
    private static WriteBatchOperation TagOp(string id, string? projectPath = null)
        => new() { OperationId = id, Operation = "create_tag", TableName = "IO", Name = "n_" + id, DataType = "Bool", ProjectPath = projectPath };

    private static WriteBatchOperation GroupOp(string id)
        => new() { OperationId = id, Operation = "create_block_group", GroupPath = "PLC_1/Blocks/G1" };

    [Fact]
    public void Valid_Batch_With_Every_Whitelisted_Operation_Passes()
    {
        var result = WriteBatchCatalog.Validate(new List<WriteBatchOperation>
        {
            new() { OperationId = "a", Operation = "create_tag_table", TableName = "IO" },
            new() { OperationId = "b", Operation = "delete_tag_table", TableName = "IO" },
            TagOp("c"),
            new() { OperationId = "d", Operation = "update_tag", TableName = "IO", Name = "n_c" },
            new() { OperationId = "e", Operation = "delete_tag", TableName = "IO", Name = "n_c" },
            new() { OperationId = "f", Operation = "create_user_constant", TableName = "IO", Name = "k", DataType = "Int", Value = "5" },
            new() { OperationId = "g", Operation = "update_user_constant", TableName = "IO", Name = "k" },
            new() { OperationId = "h", Operation = "delete_user_constant", TableName = "IO", Name = "k" },
            GroupOp("i"),
            new() { OperationId = "j", Operation = "delete_block_group", GroupPath = "PLC_1/Blocks/G1" },
        });

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Empty_Batch_Is_Rejected()
    {
        var result = WriteBatchCatalog.Validate(new List<WriteBatchOperation>());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Error.Contains("empty"));
    }

    [Fact]
    public void Unknown_Operation_Is_Rejected_With_The_Whitelist()
    {
        // update_block_logic is deliberately NOT batchable (heavier preview of its own)
        var result = WriteBatchCatalog.Validate(new List<WriteBatchOperation>
        {
            new() { OperationId = "a", Operation = "update_block_logic" },
        });

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Contains("update_block_logic", error.Error);
        Assert.Contains("create_tag", error.Error); // whitelist is named so the caller can fix it
    }

    [Fact]
    public void Missing_Required_Field_Error_Names_Field_And_Operation()
    {
        // create_user_constant without value
        var result = WriteBatchCatalog.Validate(new List<WriteBatchOperation>
        {
            new() { OperationId = "a", Operation = "create_user_constant", TableName = "IO", Name = "k", DataType = "Int" },
        });

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Contains("create_user_constant", error.Error);
        Assert.Contains("value", error.Error);
    }

    [Fact]
    public void Block_Group_Op_Without_GroupPath_Is_Rejected()
    {
        var result = WriteBatchCatalog.Validate(new List<WriteBatchOperation>
        {
            new() { OperationId = "a", Operation = "delete_block_group" },
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Error.Contains("groupPath"));
    }

    [Fact]
    public void Batch_Over_MaxBatchSize_Is_Rejected()
    {
        var items = Enumerable.Range(0, WriteBatchCatalog.MaxBatchSize + 1)
            .Select(i => TagOp("op" + i))
            .ToList();

        var result = WriteBatchCatalog.Validate(items);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Error.Contains("11") && e.Error.Contains(WriteBatchCatalog.MaxBatchSize.ToString()));
    }

    [Fact]
    public void Duplicate_OperationId_Is_Rejected_On_The_Second_Occurrence()
    {
        var result = WriteBatchCatalog.Validate(new List<WriteBatchOperation>
        {
            TagOp("dup"),
            TagOp("dup"),
        });

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Contains("duplicate", error.Error);
        Assert.Equal(1, error.Index);
    }

    [Fact]
    public void Mixed_ProjectPaths_Are_Rejected()
    {
        var result = WriteBatchCatalog.Validate(new List<WriteBatchOperation>
        {
            TagOp("a", @"C:\proj\one.ap18"),
            TagOp("b", @"C:\proj\two.ap18"),
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Error.Contains("same project"));
    }

    [Fact]
    public void Null_And_Explicit_ProjectPaths_Coexist_And_Resolve_To_The_Explicit_One()
    {
        var items = new List<WriteBatchOperation>
        {
            TagOp("a", @"C:\proj\one.ap18"),
            TagOp("b"),
        };

        var result = WriteBatchCatalog.Validate(items);
        Assert.True(result.IsValid);

        var resolved = WriteBatchCatalog.ResolveProjectPath(items, out var error);
        Assert.Null(error);
        Assert.Equal(@"C:\proj\one.ap18", resolved);
    }

    [Fact]
    public void AllNull_ProjectPaths_Resolve_To_Null_Active_Project()
    {
        var items = new List<WriteBatchOperation> { TagOp("a"), TagOp("b") };

        Assert.Null(WriteBatchCatalog.ResolveProjectPath(items, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void Operation_Names_Are_Case_Insensitive_And_Classified()
    {
        Assert.True(WriteBatchCatalog.IsBlockGroupOperation("CREATE_BLOCK_GROUP"));
        Assert.True(WriteBatchCatalog.IsBlockGroupOperation("delete_block_group"));
        Assert.False(WriteBatchCatalog.IsBlockGroupOperation("create_tag"));
        Assert.True(WriteBatchCatalog.IsTagFamilyOperation("update_tag"));
        Assert.False(WriteBatchCatalog.IsTagFamilyOperation("create_block_group"));
    }
}
