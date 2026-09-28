using System;
using System.Linq;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.Worker;

/// <summary>
/// Renders a <see cref="TagTableInfo"/> as canonical, diff-friendly text so tag tables can
/// ride the SAME name-keyed compare pipeline as blocks (compare_plc_blocks): one line per
/// tag and per user constant, members sorted by name so a reorder in TIA is not reported as
/// a change. Pure (Contracts-only) so the exact rendering is unit-testable — both sides of
/// the compare run through the same renderer, so line noise cancels out.
/// </summary>
public static class TagTableCanonicalText
{
    public static string Render(TagTableInfo table)
    {
        var lines = new System.Collections.Generic.List<string>
        {
            $"TABLE \"{table.Name}\" ({(string.IsNullOrEmpty(table.FolderPath) ? "/" : table.FolderPath)})",
        };

        if (table.IsDefault)
        {
            lines.Add("DEFAULT");
        }

        foreach (var tag in table.Tags.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            var comment = string.IsNullOrWhiteSpace(tag.Comment) ? "" : $"  // {tag.Comment.Replace("\n", " ").Replace("\r", "")}";
            lines.Add($"  TAG \"{tag.Name}\" : {tag.DataType} := {tag.LogicalAddress}{comment}");
        }

        foreach (var constant in table.UserConstants.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"  CONSTANT \"{constant.Name}\" : {constant.DataType} := {constant.Value}");
        }

        return string.Join("\n", lines);
    }
}
