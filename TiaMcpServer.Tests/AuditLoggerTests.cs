using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Audit;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// Unit tests for the pure JSONL audit trail: file naming per UTC day, one camelCase JSON line
/// per write, and the best-effort contract — an audit failure must NEVER throw into the
/// operation being audited. All tests write into temp directories (the injectable path).
/// </summary>
public class AuditLoggerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tia-audit-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch
        {
            // temp cleanup is best-effort
        }
    }

    private string[] ReadLines()
    {
        var file = Path.Combine(_dir, AuditLogger.FileNameFor(DateTimeOffset.UtcNow));
        Assert.True(File.Exists(file), $"expected audit file {file}");
        return File.ReadAllLines(file);
    }

    [Fact]
    public void FileName_Uses_Utc_Day_Without_Dashes()
    {
        Assert.Equal("audit-20260909.jsonl", AuditLogger.FileNameFor(new DateTimeOffset(2026, 9, 9, 23, 59, 0, TimeSpan.Zero)));
        // a +2h local offset before midnight UTC still lands on the UTC day
        Assert.Equal("audit-20260909.jsonl",
            AuditLogger.FileNameFor(new DateTimeOffset(2026, 9, 10, 1, 30, 0, TimeSpan.FromHours(2))));
    }

    [Fact]
    public void Append_Writes_One_CamelCase_Json_Line_With_All_Fields()
    {
        AuditLogger.Append("apply_write_batch", "create_tag", "PLC tag 'n_a' in table 'IO'.",
            "applied", batchId: "wb-1a2b3c4d", directory: _dir);

        var lines = ReadLines();
        var json = Assert.Single(lines);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("apply_write_batch", root.GetProperty("tool").GetString());
        Assert.Equal("create_tag", root.GetProperty("operation").GetString());
        Assert.Equal("PLC tag 'n_a' in table 'IO'.", root.GetProperty("target").GetString());
        Assert.Equal("applied", root.GetProperty("outcome").GetString());
        Assert.Equal("wb-1a2b3c4d", root.GetProperty("batchId").GetString());
        // timestamp must be a real, recent UTC instant
        var timestamp = root.GetProperty("timestamp").GetDateTimeOffset();
        Assert.True(Math.Abs((timestamp - DateTimeOffset.UtcNow).TotalMinutes) < 5);
    }

    [Fact]
    public void Append_Accumulates_Lines_In_One_File()
    {
        AuditLogger.Append("apply_write_batch", "create_tag", "t1", "applied", "wb-1", _dir);
        AuditLogger.Append("apply_write_batch", "delete_tag", "t2", "failed", "wb-1", _dir);
        AuditLogger.Append("apply_write_batch", "create_tag", "t3", "skipped", "wb-1", _dir);
        AuditLogger.Append("delete_block_group", "delete_block_group", "g1", "applied", null, _dir);

        var lines = ReadLines();
        Assert.Equal(4, lines.Length);
        Assert.All(lines, l => Assert.EndsWith("}", l.TrimEnd()));
    }

    [Fact]
    public void Append_Creates_The_Directory_When_Missing()
    {
        var nested = Path.Combine(_dir, "deeply", "nested");
        AuditLogger.Append("apply_write_batch", "create_tag", "t", "applied", null, nested);

        Assert.True(Directory.Exists(nested));
        Assert.Single(Directory.GetFiles(nested, "audit-*.jsonl"));
    }

    [Fact]
    public void Append_Never_Throws_On_An_Unwritable_Path()
    {
        // a path that cannot exist as a directory (illegal chars on Windows) — Append must
        // swallow it: an audit failure must not fail the operation being audited
        var invalid = _dir + "\0|invalid";

        var ex = Record.Exception(() => AuditLogger.Append("apply_write_batch", "create_tag", "t", "applied", null, invalid));

        Assert.Null(ex);
    }

    [Fact]
    public void Append_Omits_Null_BatchId_Cleanly()
    {
        AuditLogger.Append("delete_block_group", "delete_block_group", "g", "applied", null, _dir);

        using var doc = JsonDocument.Parse(Assert.Single(ReadLines()));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("batchId").ValueKind);
        Assert.Equal("applied", doc.RootElement.GetProperty("outcome").GetString());
    }

    [Fact]
    public void Append_Never_Throws_When_The_File_Is_Locked_By_A_Directory()
    {
        // the audit "file" path is an existing DIRECTORY → AppendAllText must fail → swallowed
        var day = AuditLogger.FileNameFor(DateTimeOffset.UtcNow);
        var fileAsDir = Path.Combine(_dir, day);
        Directory.CreateDirectory(_dir);
        Directory.CreateDirectory(fileAsDir);

        var ex = Record.Exception(() => AuditLogger.Append("apply_write_batch", "create_tag", "t", "applied", null, _dir));

        Assert.Null(ex);
    }
}
