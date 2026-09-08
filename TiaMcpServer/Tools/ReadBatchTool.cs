using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class ReadBatchTool
{
    [McpServerTool(Name = "read_batch")]
    [Description(
        "Run N read operations in ONE call through the persistent TIA worker — version routing, " +
        "export fallback chain and source reconstruction included. Whitelisted operations " +
        "(required fields): get_block_content(blockPath — plcName not used, put the PLC in the " +
        "path like 'PLC_1/FC100'), read_block_interface(blockPath), list_blocks(none), " +
        "list_tag_tables(none), find_tags(query), search_code(query, optional contextLines), " +
        "tag_usage(tag). Every item also accepts operationId (required, echoed back), plcName " +
        "(optional scoping where the op supports it), projectPath, tiaVersion. Operations run " +
        "sequentially with per-item isolation: each returns status succeeded|failed|omitted + ms " +
        "+ result, and one failure never aborts the rest. Payload caps: each result truncates " +
        "at 20k chars; once the batch exceeds 150k chars, later items are omitted (re-run with " +
        "fewer or narrower operations). Max 25 items.")]
    public static async Task<string> ReadBatch(
        OpennessWorkerClient workerClient,
        [Description("Operations to run (1-25 items).")] List<ReadBatchOperation> operations)
        => await workerClient.ReadBatchAsync(operations).ConfigureAwait(false);
}
