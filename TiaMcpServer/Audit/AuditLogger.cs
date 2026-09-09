using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TiaMcpServer.Audit;

/// <summary>
/// Best-effort JSONL audit trail for applied writes: one line per executed write operation,
/// appended to <c>%LOCALAPPDATA%\TiaMcpServer\audit\audit-&lt;yyyyMMdd&gt;.jsonl</c> (one file per
/// UTC day; deliberately a DIFFERENT filename pattern from WriteSafetyService's per-tool
/// <c>&lt;yyyy-MM-dd&gt;.jsonl</c> so the two trails coexist). Pure (Siemens-free) with an
/// injectable directory so tests write to temp files. An audit-write failure NEVER fails the
/// operation being audited — it is reported on stderr and swallowed.
/// </summary>
public static class AuditLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>File name for a UTC day, e.g. <c>audit-20260909.jsonl</c>.</summary>
    public static string FileNameFor(DateTimeOffset timestampUtc)
        => $"audit-{timestampUtc.UtcDateTime:yyyyMMdd}.jsonl";

    /// <summary>Default audit directory under %LOCALAPPDATA% (falls back to the temp dir when
    /// LocalApplicationData is unresolvable, e.g. some service accounts).</summary>
    public static string DefaultDirectory()
    {
        try
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TiaMcpServer",
                "audit");
        }
        catch
        {
            return Path.GetTempPath();
        }
    }

    /// <summary>
    /// Append one audit line: {timestamp, tool, operation, target, outcome, batchId?}. Best-effort —
    /// never throws (an audit failure must not fail the write it records); reports on stderr.
    /// Outcome vocabulary: applied | failed | skipped.
    /// </summary>
    public static void Append(
        string tool,
        string operation,
        string target,
        string outcome,
        string? batchId = null,
        string? directory = null)
    {
        try
        {
            var dir = directory ?? DefaultDirectory();
            Directory.CreateDirectory(dir);
            var timestamp = DateTimeOffset.UtcNow;
            var line = JsonSerializer.Serialize(
                new
                {
                    timestamp,
                    tool,
                    operation,
                    target,
                    outcome,
                    batchId,
                },
                JsonOptions);
            File.AppendAllText(
                Path.Combine(dir, FileNameFor(timestamp)),
                line + Environment.NewLine,
                Encoding.UTF8);
        }
        catch (Exception ex)
        {
            // Best-effort by contract: stderr only, and even that must not throw.
            try
            {
                Console.Error.WriteLine($"[audit] failed to append audit line for {tool}/{operation}: {ex.Message}");
            }
            catch
            {
                // ignore — never propagate an audit failure into the audited operation
            }
        }
    }
}
