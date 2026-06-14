using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.SW.Tags;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Full-text search across PLC block source code, plus tag-usage lookup.
/// Both build a <see cref="BlockCodeIndexer"/> index once and scan it.
/// </summary>
public static class CodeSearcher
{
    private const int DefaultContextLines = 2;
    private const int MaxMatches = 200;

    public static CodeSearchResultInfo Search(
        Project project, string? projectPath, string? plcNameFilter, string pattern, bool ignoreCase, int contextLines)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return new CodeSearchResultInfo { Pattern = pattern ?? string.Empty, PlcName = plcNameFilter };
        }

        contextLines = contextLines < 0 ? 0 : Math.Min(contextLines, 10);
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var pat = pattern.Trim();
        var index = BlockCodeIndexer.GetOrBuild(project, projectPath, plcNameFilter);

        var result = new CodeSearchResultInfo
        {
            Pattern = pattern,
            PlcName = plcNameFilter,
            SearchedBlockCount = index.Blocks.Count,
            SkippedProtectedCount = index.SkippedProtected,
        };

        foreach (var blk in index.Blocks)
        {
            for (var i = 0; i < blk.Lines.Count; i++)
            {
                if (blk.Lines[i].IndexOf(pat, comparison) < 0)
                {
                    continue;
                }

                result.Matches.Add(BuildMatch(blk, i, contextLines));
                if (result.Matches.Count >= MaxMatches)
                {
                    result.MatchCount = result.Matches.Count;
                    return result;
                }
            }
        }

        result.MatchCount = result.Matches.Count;
        return result;
    }

    public static TagUsageResultInfo TagUsage(Project project, string? projectPath, string? plcNameFilter, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return new TagUsageResultInfo { Tag = tag ?? string.Empty, PlcName = plcNameFilter };
        }

        var tagName = tag.Trim();
        var index = BlockCodeIndexer.GetOrBuild(project, projectPath, plcNameFilter);

        // Resolve the tag's logical address(es) so we ALSO catch absolute-address
        // references in STL / older code, where the symbolic name never appears
        // (e.g. tag "AFPAKKER_INSTALLATIE_DRAAIT" at %I301.0 is used as "E 301.0").
        var addresses = ResolveTagAddresses(project, plcNameFilter, tagName);

        var terms = new List<string> { tagName };
        var variantSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var addr in addresses)
        {
            foreach (var v in AddressVariants(addr))
            {
                if (variantSet.Add(v))
                {
                    terms.Add(v);
                }
            }
        }

        return TagUsageInIndex(
            index, plcNameFilter, tag, terms, addresses,
            variantSet.OrderBy(t => t).ToList());
    }

    /// <summary>Search a pre-built index for references to a tag (reuses the index
    /// so many tags can be traced without re-exporting block source each time).</summary>
    internal static TagUsageResultInfo TagUsageInIndex(
        IndexBuildResult index, string? plcNameFilter, string tag,
        List<string>? terms = null, List<string>? addresses = null,
        List<string>? variantsTried = null)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return new TagUsageResultInfo { Tag = tag ?? string.Empty, PlcName = plcNameFilter };
        }

        var searchTerms = terms ?? new List<string> { tag.Trim() };
        // Try longer/more-specific terms first so MatchedTerm reports the most precise hit.
        var orderedTerms = searchTerms.OrderByDescending(t => t.Length).ToList();

        var result = new TagUsageResultInfo
        {
            Tag = tag,
            PlcName = plcNameFilter,
            Addresses = addresses ?? new List<string>(),
            AddressVariantsTried = variantsTried ?? new List<string>(),
            SearchedBlockCount = index.Blocks.Count,
            SkippedProtectedCount = index.SkippedProtected,
        };

        foreach (var blk in index.Blocks)
        {
            for (var i = 0; i < blk.Lines.Count; i++)
            {
                var line = blk.Lines[i];
                string? matched = null;
                foreach (var t in orderedTerms)
                {
                    if (line.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matched = t;
                        break;
                    }
                }

                if (matched == null)
                {
                    continue;
                }

                result.References.Add(new TagReferenceInfo
                {
                    PlcName = blk.PlcName,
                    BlockName = blk.BlockName,
                    BlockType = blk.BlockType,
                    LineNumber = i + 1,
                    Line = line.TrimEnd(),
                    Access = ClassifyAccess(blk.Lines, i, matched),
                    MatchedTerm = matched,
                });
                if (result.References.Count >= MaxMatches)
                {
                    result.ReferenceCount = result.References.Count;
                    return result;
                }
            }
        }

        result.ReferenceCount = result.References.Count;
        return result;
    }

    /// <summary>Resolve the logical address(es) of a tag by scanning tag tables.</summary>
    private static List<string> ResolveTagAddresses(Project project, string? plcNameFilter, string tagName)
    {
        var addrs = new List<string>();
        try
        {
            foreach (var (_, plc) in PlcSoftwareFinder.Filter(project, plcNameFilter))
            {
                CollectTagAddresses(plc.TagTableGroup, tagName, addrs);
            }
        }
        catch (EngineeringException)
        {
            /* ignore — fall back to name-only search */
        }

        return addrs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void CollectTagAddresses(PlcTagTableGroup group, string tagName, List<string> addrs)
    {
        foreach (PlcTagTable table in group.TagTables)
        {
            try
            {
                foreach (PlcTag tag in table.Tags)
                {
                    try
                    {
                        if (string.Equals(tag.Name, tagName, StringComparison.OrdinalIgnoreCase))
                        {
                            addrs.Add(tag.LogicalAddress);
                        }
                    }
                    catch (EngineeringException) { /* skip unreadable tag */ }
                }
            }
            catch (EngineeringException) { /* skip unreadable table */ }
        }

        foreach (PlcTagTableGroup child in group.Groups)
        {
            CollectTagAddresses(child, tagName, addrs);
        }
    }

    /// <summary>
    /// Address forms a tag may take in block code: with/without the leading '%', with a
    /// space after the area letter, and the German-area equivalent (I→E Eingang, Q→A Ausgang)
    /// since STL often uses German mnemonics.
    /// e.g. "%I301.0" → { "%I301.0", "I301.0", "I 301.0", "E301.0", "E 301.0" }
    /// </summary>
    private static IEnumerable<string> AddressVariants(string addr)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(addr))
        {
            return set;
        }

        var raw = addr.Trim();
        set.Add(raw);

        var noPercent = raw.TrimStart('%');
        if (noPercent.Length == 0)
        {
            return set;
        }

        set.Add(noPercent);

        var split = 0;
        while (split < noPercent.Length && char.IsLetter(noPercent[split]))
        {
            split++;
        }

        var letters = noPercent.Substring(0, split);
        var rest = noPercent.Substring(split);
        if (letters.Length == 0 || rest.Length == 0)
        {
            return set;
        }

        set.Add(letters + " " + rest); // e.g. "I 301.0", "IW 302"

        var germanFirst = letters[0] switch
        {
            'I' => 'E', 'i' => 'e',
            'Q' => 'A', 'q' => 'a',
            var c => c,
        };
        var germanLetters = germanFirst + letters.Substring(1);
        set.Add(germanLetters + rest);
        set.Add(germanLetters + " " + rest);

        return set;
    }

    private static CodeMatchInfo BuildMatch(IndexedBlock blk, int lineIndex, int contextLines)
    {
        var match = new CodeMatchInfo
        {
            PlcName = blk.PlcName,
            BlockName = blk.BlockName,
            BlockType = blk.BlockType,
            ProgrammingLanguage = blk.ProgrammingLanguage,
            LineNumber = lineIndex + 1,
            Line = blk.Lines[lineIndex].TrimEnd(),
        };

        for (var c = 1; c <= contextLines; c++)
        {
            var before = lineIndex - c;
            if (before >= 0)
            {
                match.ContextBefore.Insert(0, blk.Lines[before].TrimEnd());
            }

            var after = lineIndex + c;
            if (after < blk.Lines.Count)
            {
                match.ContextAfter.Add(blk.Lines[after].TrimEnd());
            }
        }

        return match;
    }

    /// <summary>
    /// Best-effort read/write classification:
    /// - SCL source: ':=' with the tag on the LEFT → write.
    /// - STL source text: a line beginning with 'T ' (transfer) → write.
    /// - Openness XML export (STL): the instruction is the &lt;StlToken Text="X"/&gt; a few
    ///   lines above the operand &lt;Component&gt;. Look back for it and map known write
    ///   instructions (Assign/=, Transfer/T, Set/S, Reset/R) → write; reads (A/AN/O/L/…)
    ///   → read; if the operand is XML but no instruction is recoverable → "unknown".
    /// This is a heuristic — the full line text is always returned so the caller can verify.
    /// </summary>
    private static string ClassifyAccess(IReadOnlyList<string> lines, int index, string tagName)
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

        // Plain STL source text OR reconstructed readable STL: a line beginning with a write
        // mnemonic (= assign, T transfer, S set, R reset) writes the operand. Reads
        // (A / AN / O / ON / L / ...) do not match here and fall through to "read".
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

        // XML operand with no recoverable instruction — don't guess "read".
        if (line.IndexOf("<Component", StringComparison.OrdinalIgnoreCase) >= 0
            || line.IndexOf("<Symbol", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "unknown";
        }

        return "read";
    }

    /// <summary>Look back up to ~15 lines for the nearest &lt;StlToken Text="X"/&gt; — the
    /// STL instruction that owns the operand at <paramref name="index"/>. Each STL statement
    /// has exactly one StlToken immediately above its operand Access, so the nearest one is it.</summary>
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

    /// <summary>TIA Openness STL instruction tokens that WRITE their operand: the spelled
    /// names (Assign/Transfer/Set/Reset) and the raw mnemonics (= / T / S / R).</summary>
    private static bool IsWriteInstruction(string token)
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

    /// <summary>True when a (left-trimmed) reconstructed/STL source line begins with a single
    /// write mnemonic — <c>=</c> (assign), <c>T</c>/<c>t</c> (transfer), <c>S</c>/<c>s</c> (set),
    /// <c>R</c>/<c>r</c> (reset) — followed by whitespace, a quote, or end-of-line. The
    /// whitespace guard prevents matching read/bit-test instructions that share a leading letter
    /// (e.g. <c>SLD</c>, <c>RND</c>). Reads (<c>A</c>/<c>AN</c>/<c>O</c>/<c>ON</c>/<c>L</c>/…)
    /// are intentionally not matched.</summary>
    private static bool StartsWriteMnemonic(string s)
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
}
