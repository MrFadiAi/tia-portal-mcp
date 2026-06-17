using System;
using System.Collections.Generic;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Pure (Siemens-free) filtering over an already-read <see cref="TagTableInfo"/> list. Used by the
/// structured tag-table export (<c>export_tag_table_json</c>) to honor <c>tableName</c>/
/// <c>folderPath</c> without widening <see cref="TagTableReader.ReadAll"/>'s signature (which reads
/// every table under a PLC). Link-compiled into the test project so the filter is unit-testable.
/// </summary>
internal static class TagTableFilter
{
    /// <summary>
    /// Keep only tag tables whose folder path matches <paramref name="folderPath"/>. A null/blank
    /// folderPath returns every table (passthrough). Matching is case-insensitive and ignores leading
    /// and trailing slashes, so <c>"MERKERS"</c>, <c>"/MERKERS"</c> and <c>"/MERKERS/"</c> are
    /// equivalent; <c>"/"</c> matches only root-level tables.
    /// </summary>
    public static List<TagTableInfo> ByFolder(List<TagTableInfo> tables, string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return tables;
        }

        var normalized = NormalizePath(folderPath);
        var result = new List<TagTableInfo>();
        foreach (var table in tables)
        {
            if (string.Equals(NormalizePath(table.FolderPath), normalized, StringComparison.Ordinal))
            {
                result.Add(table);
            }
        }
        return result;
    }

    /// <summary>
    /// Keep only tag tables whose name matches <paramref name="tableName"/> (case-insensitive).
    /// A null/blank tableName returns every table (passthrough).
    /// </summary>
    public static List<TagTableInfo> ByName(List<TagTableInfo> tables, string? tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return tables;
        }

        var result = new List<TagTableInfo>();
        foreach (var table in tables)
        {
            if (string.Equals(table.Name, tableName, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(table);
            }
        }
        return result;
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty; // root
        }
        return path.Trim().Trim('/').ToLowerInvariant();
    }
}
