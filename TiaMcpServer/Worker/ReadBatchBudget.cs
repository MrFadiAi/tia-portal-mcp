using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Worker;

public sealed class ReadBatchOperationResult
{
    public string OperationId { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Status { get; set; } = ""; // succeeded | failed | previewed | skipped (write batch)
    public long Ms { get; set; }
    public string Result { get; set; } = "";
    /// <summary>Optional structured warnings (write batch). Null for read_batch items; the
    /// payload ladder truncates these AFTER failure details — never before.</summary>
    public List<string>? Warnings { get; set; }
}

/// <summary>
/// Payload caps for batch results (read_batch, preview_write_batch, apply_write_batch). Pure
/// (Siemens-free) so it is link-compiled into the test project. Two layers:
/// <see cref="ApplyItemCap"/> bounds ONE item (applied as results are collected), then
/// <see cref="ApplyBatchLadder"/> bounds the whole batch through a failure-preserving
/// degradation ladder (ported from upstream's OperationBatchPayloadBudget): when the combined
/// response exceeds <see cref="MaxBatchChars"/>, degrade in order —
/// (1) succeeded payloads are replaced by a marker, (2) failure DETAILS are truncated
/// (head kept), (3) WARNINGS are collapsed — so failure information is never dropped to make
/// room for successes, and a failed item keeps its status and the head of its error at every
/// step. The ladder never throws and never omits a failed item; the terminal clamp is the
/// only unreachable-in-practice fallback that hard-fits every item.
/// </summary>
public static class ReadBatchBudget
{
    public const int MaxItemChars = 20_000;
    public const int MaxBatchChars = 150_000;

    /// <summary>When a failure's detail must be truncated (ladder step 2), this many leading
    /// chars survive — enough for "Error: …" plus context; for write-batch failures the
    /// leading stop-warning always fits inside it.</summary>
    public const int FailureDetailRescueChars = 2_000;

    public const string OmittedSuccessMarker =
        "payload omitted for budget — op succeeded";

    public const string WarningsTruncationMarker =
        "[warnings truncated for budget — failure detail was preserved first]";

    /// <summary>Cap one result: truncated to <see cref="MaxItemChars"/> plus a marker naming
    /// the original total, so the caller knows content was cut.</summary>
    public static string ApplyItemCap(string result)
        => result.Length <= MaxItemChars
            ? result
            : result[..MaxItemChars] + $"\n...[truncated, {result.Length} chars total]";

    /// <summary>
    /// The batch-level failure-preserving ladder, applied AFTER all operations ran. Budget
    /// accounting covers each item's Result AND its Warnings. Steps run in order and each stops
    /// as soon as the batch fits:
    /// (1) non-failed payloads (earliest first) are replaced by <see cref="OmittedSuccessMarker"/>
    ///     — the status stays "succeeded"/"previewed"/"skipped": the operation ran fine, only
    ///     its payload is gone;
    /// (2) failed items' details (in order) are truncated to the
    ///     <see cref="FailureDetailRescueChars"/> head plus a marker;
    /// (3) warning lists are collapsed to <see cref="WarningsTruncationMarker"/>;
    /// (4) terminal clamp — hard-fit every item to an equal share of the budget. Only
    ///     reachable with pathologically many huge items; it keeps every item's status and the
    ///     head of its result so a failure can never be silently dropped (upstream throws here).
    /// </summary>
    public static void ApplyBatchLadder(List<ReadBatchOperationResult> items)
    {
        if (TotalChars(items) <= MaxBatchChars)
        {
            return;
        }

        // Step 1 — success payloads are the cheapest to drop and the least informative.
        foreach (var item in items)
        {
            if (TotalChars(items) <= MaxBatchChars)
            {
                break;
            }

            if (item.Status == "failed")
            {
                continue;
            }

            item.Result = OmittedSuccessMarker;
            item.Warnings = null;
        }

        // Step 2 — truncate failure details, keeping the head (where "Error:" and the write-batch
        // stop-warning live).
        foreach (var item in items)
        {
            if (TotalChars(items) <= MaxBatchChars)
            {
                break;
            }

            if (item.Status != "failed" || item.Result.Length <= FailureDetailRescueChars)
            {
                continue;
            }

            item.Result = item.Result[..FailureDetailRescueChars] +
                          $"\n[failure detail truncated for budget — {item.Result.Length} chars total]";
        }

        // Step 3 — collapse warnings (only reached when failure details are already at their
        // rescue size, so warnings are truncated AFTER failure details, never instead of them).
        foreach (var item in items)
        {
            if (TotalChars(items) <= MaxBatchChars)
            {
                break;
            }

            if (item.Warnings is null || item.Warnings.Count == 0)
            {
                continue;
            }

            item.Warnings = new List<string> { WarningsTruncationMarker };
        }

        // Step 4 — terminal clamp. Degrade items (in order) to an equal share of the budget:
        // head of the result plus a marker, warnings dropped. Unreachable for real batch sizes
        // (≤ 25 items × rescue-sized details fit comfortably); exists so the ladder NEVER
        // throws and no failed item is ever dropped outright.
        var keep = Math.Max(200, MaxBatchChars / Math.Max(1, items.Count) - 100);
        foreach (var item in items)
        {
            if (TotalChars(items) <= MaxBatchChars)
            {
                break;
            }

            if (item.Result.Length > keep)
            {
                item.Result = item.Result[..keep] + "\n[truncated to fit the batch budget]";
            }

            item.Warnings = null;
        }
    }

    /// <summary>Total accounted chars: every item's result plus its warnings. The ladder's
    /// budget check; matches the per-item cap convention of counting emitted payload chars.</summary>
    public static int TotalChars(IReadOnlyList<ReadBatchOperationResult> items)
        => items.Sum(i => (i.Result?.Length ?? 0) + (i.Warnings?.Sum(w => w.Length) ?? 0));
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
