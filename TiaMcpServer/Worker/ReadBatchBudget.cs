using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Worker;

public sealed class ReadBatchOperationResult
{
    public string OperationId { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Status { get; set; } = ""; // succeeded | failed | omitted | skipped (write batch)
    public long Ms { get; set; }
    public string Result { get; set; } = "";
    /// <summary>Optional structured warnings (write batch). Null for read_batch items; the
    /// payload ladder truncates these BEFORE dropping any failure information.</summary>
    public List<string>? Warnings { get; set; }
}

/// <summary>
/// Payload caps for read_batch results. Pure (Siemens-free) so it is link-compiled into the
/// test project. Applied by the orchestrator AFTER all operations ran: first the per-item cap,
/// then the batch cap (which only ever omits non-failed items — failures are small error
/// strings and must always reach the caller).
/// </summary>
public static class ReadBatchBudget
{
    public const int MaxItemChars = 20_000;
    public const int MaxBatchChars = 150_000;
    public const string ExhaustedMessage =
        "batch payload budget exhausted — re-run with fewer or narrower operations";

    /// <summary>Cap one result: truncated to <see cref="MaxItemChars"/> plus a marker naming
    /// the original total, so the caller knows content was cut.</summary>
    public static string ApplyItemCap(string result)
        => result.Length <= MaxItemChars
            ? result
            : result[..MaxItemChars] + $"\n...[truncated, {result.Length} chars total]";

    /// <summary>
    /// Walk items in order keeping a running total of emitted chars; once the accumulated total
    /// would exceed <see cref="MaxBatchChars"/>, this and every later NON-failed item becomes
    /// <c>omitted</c> with the exhaustion hint (earlier items keep their full results). Failed
    /// items are never omitted — their error text is how the caller learns what went wrong.
    /// </summary>
    public static void ApplyBatchCap(List<ReadBatchOperationResult> items)
    {
        var total = 0;
        var exhausted = false;
        foreach (var item in items)
        {
            if (item.Status == "failed")
            {
                total += item.Result?.Length ?? 0;
                continue;
            }

            var len = item.Result?.Length ?? 0;
            if (exhausted || total + len > MaxBatchChars)
            {
                exhausted = true;
                item.Status = "omitted";
                item.Result = ExhaustedMessage;
                continue;
            }

            total += len;
        }
    }
}

/// <summary>Pure response-shape builder for read_batch: the counts summary plus one entry per
/// operation (id, op, status, ms, result), serialized camelCase by the caller.</summary>
public static class ReadBatchResponse
{
    public static object For(IReadOnlyList<ReadBatchOperationResult> operations)
        => new
        {
            tool = "read_batch",
            operationCount = operations.Count,
            succeeded = operations.Count(o => o.Status == "succeeded"),
            failed = operations.Count(o => o.Status == "failed"),
            omitted = operations.Count(o => o.Status == "omitted"),
            operations = operations.Select(o => new
            {
                o.OperationId, o.Operation, o.Status, o.Ms, o.Result,
            }).ToList(),
        };
}
