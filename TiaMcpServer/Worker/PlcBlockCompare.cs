using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Worker;

/// <summary>One block's identity + reconstructed source, the input to the comparison.</summary>
public sealed class BlockInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Source { get; set; } = "";
}

public sealed class ChangedBlock
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string SourceA { get; set; } = "";
    public string SourceB { get; set; } = "";
    public string? Note { get; set; }
}

public sealed class CompareResult
{
    public List<BlockInfo> Added { get; set; } = new();
    public List<BlockInfo> Removed { get; set; } = new();
    public List<ChangedBlock> Changed { get; set; } = new();
    public List<BlockInfo> Unchanged { get; set; } = new();
}

/// <summary>Pure roster + source diff between two PLCs. No Siemens/Openness deps.</summary>
public static class PlcBlockCompare
{
    /// <summary>Normalize reconstructed source for equality: trim trailing ws per line,
    /// drop trailing blank lines. Reconstruction is deterministic so this is stable.</summary>
    public static string NormalizeSource(string source)
    {
        if (string.IsNullOrEmpty(source)) return "";
        var lines = source.Replace("\r\n", "\n").Split('\n')
            .Select(l => l.TrimEnd());
        lines = lines.Reverse().SkipWhile(string.IsNullOrEmpty).Reverse();
        return string.Join("\n", lines);
    }

    public static CompareResult Compare(IReadOnlyList<BlockInfo> sideA, IReadOnlyList<BlockInfo> sideB)
    {
        var result = new CompareResult();
        var aBy = sideA.ToDictionary(b => b.Name.ToLowerInvariant());
        var bBy = sideB.ToDictionary(b => b.Name.ToLowerInvariant());

        foreach (var a in sideA)
        {
            var key = a.Name.ToLowerInvariant();
            if (!bBy.TryGetValue(key, out var b))
            {
                result.Added.Add(a);
                continue;
            }

            bool typeMismatch = !string.Equals(a.Type, b.Type, System.StringComparison.OrdinalIgnoreCase);
            bool sourceEqual = string.Equals(NormalizeSource(a.Source), NormalizeSource(b.Source), System.StringComparison.Ordinal);

            if (sourceEqual && !typeMismatch)
            {
                result.Unchanged.Add(a);
            }
            else
            {
                result.Changed.Add(new ChangedBlock
                {
                    Name = a.Name,
                    Type = typeMismatch ? $"{a.Type}/{b.Type}" : a.Type,
                    SourceA = a.Source,
                    SourceB = b.Source,
                    Note = typeMismatch ? $"type-mismatch: {a.Type}->{b.Type}" : null,
                });
            }
        }

        foreach (var b in sideB)
        {
            var key = b.Name.ToLowerInvariant();
            if (!aBy.ContainsKey(key)) result.Removed.Add(b);
        }

        return result;
    }
}
