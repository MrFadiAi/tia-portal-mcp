using System;
using System.Collections.Generic;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Pure (Siemens-free) classifier for a single source line, telling <c>tag_usage</c> whether a
/// matched line is a <c>read</c> / <c>write</c> / <c>unknown</c> reference -- or a non-reference
/// (comment / network header) that must not be counted. Extracted from <see cref="CodeSearcher"/>
/// so the read/write heuristic is fully unit-testable. Works on reconstructed readable STL
/// (<c>"      =     \"TAG\""</c>) and on raw Openness XML (<c>"&lt;Component Name="TAG"/&gt;"</c>).
/// </summary>
internal static class StlAccessClassifier
{
    /// <summary>Lines that are NOT code references: reconstructed comments (<c>// ...</c>) and the
    /// reconstructor's network headers (<c>// Network N</c>). <c>tag_usage</c> must skip these so a
    /// tag name mentioned only in a comment is not miscounted as a read reference.</summary>
    public static bool IsCommentOrHeader(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        return line.TrimStart().StartsWith("//", StringComparison.Ordinal);
    }

    /// <summary>
    /// Best-effort read/write classification of a matched line:
    /// <list type="bullet">
    /// <item>SCL source: ':=' with the tag on the LEFT -> write.</item>
    /// <item>STL source text / reconstructed readable STL: a line beginning with a write mnemonic
    /// (= assign, T transfer, S set, R reset) -> write.</item>
    /// <item>Raw Openness XML (STL): the owning &lt;StlToken Text="X"/&gt; a few lines above the
    /// operand -> Assign/Transfer/Set/Reset = write, else read.</item>
    /// <item>XML operand with no recoverable instruction -> "unknown".</item>
    /// <item>Otherwise (a plain read like 'A "TAG"') -> "read".</item>
    /// </list>
    /// Heuristic -- the caller always returns the full line text so a human can verify.
    /// </summary>
    public static string Classify(IReadOnlyList<string> lines, int index, string tagName)
    {
        var line = lines[index];

        // SCL assignment: '#Tag := ...' / '"Tag" := ...'
        var assignIndex = line.IndexOf(":=", StringComparison.Ordinal);
        if (assignIndex >= 0)
        {
            var tagIndex = line.IndexOf(tagName, StringComparison.OrdinalIgnoreCase);
            if (tagIndex >= 0 && tagIndex < assignIndex)
            {
                return "write";
            }
        }

        // Plain STL source text OR reconstructed readable STL: a write mnemonic at the start.
        var trimmed = line.TrimStart();
        if (StartsWriteMnemonic(trimmed)
            && line.IndexOf(tagName, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "write";
        }

        // Openness XML export: the STL instruction owns the operand. Find it and map it.
        var token = FindStlToken(lines, index);
        if (token != null)
        {
            return IsWriteInstruction(token) ? "write" : "read";
        }

        // XML operand with no recoverable instruction -- don't guess "read".
        if (line.IndexOf("<Component", StringComparison.OrdinalIgnoreCase) >= 0
            || line.IndexOf("<Symbol", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "unknown";
        }

        return "read";
    }

    /// <summary>True when a (left-trimmed) reconstructed/STL source line begins with a single
    /// write mnemonic -- <c>=</c> (assign), <c>T</c>/<c>t</c> (transfer), <c>S</c>/<c>s</c> (set),
    /// <c>R</c>/<c>r</c> (reset) -- followed by whitespace, a quote, or end-of-line. The
    /// whitespace guard prevents matching read/bit-test instructions that share a leading letter
    /// (e.g. <c>SLD</c>, <c>RND</c>, <c>SET</c>, <c>==I</c>). Reads (<c>A</c>/<c>AN</c>/<c>O</c>/
    /// <c>ON</c>/<c>L</c>/...) are intentionally not matched.</summary>
    public static bool StartsWriteMnemonic(string s)
    {
        if (s.Length == 0)
        {
            return false;
        }

        var c = s[0];
        if (c != '=' && c != 'T' && c != 't' && c != 'S' && c != 's' && c != 'R' && c != 'r')
        {
            return false;
        }

        if (s.Length == 1)
        {
            return true;
        }

        var next = s[1];
        return char.IsWhiteSpace(next) || next == '"';
    }

    /// <summary>TIA Openness STL instruction tokens that WRITE their operand: the spelled names
    /// (Assign/Transfer/Set/Reset) and the raw mnemonics (= / T / S / R).</summary>
    public static bool IsWriteInstruction(string token)
    {
        switch (token.Trim().ToUpperInvariant())
        {
            case "ASSIGN":   // =
            case "TRANSFER": // T
            case "SET":      // S
            case "RESET":    // R
            case "=":
            case "T":
            case "S":
            case "R":
                return true;
            default:
                return false;
        }
    }

    /// <summary>Look back up to ~15 lines for the nearest &lt;StlToken Text="X"/&gt; -- the STL
    /// instruction that owns the operand at <paramref name="index"/>. Each STL statement has
    /// exactly one StlToken immediately above its operand Access, so the nearest one is it.</summary>
    private static string? FindStlToken(IReadOnlyList<string> lines, int index)
    {
        var lower = Math.Max(0, index - 15);
        for (var k = index; k >= lower; k--)
        {
            var l = lines[k];
            var ti = l.IndexOf("<StlToken", StringComparison.OrdinalIgnoreCase);
            if (ti < 0)
            {
                continue;
            }

            var qs = l.IndexOf("Text=\"", ti, StringComparison.OrdinalIgnoreCase);
            if (qs < 0)
            {
                continue;
            }

            qs += "Text=\"".Length;
            var qe = l.IndexOf('"', qs);
            if (qe > qs)
            {
                return l.Substring(qs, qe - qs);
            }
        }

        return null;
    }
}
