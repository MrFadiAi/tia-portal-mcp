using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Reconstructs readable STL source from the tokenized Openness StatementList XML, so the read
/// tools (<c>get_block_content</c>) and the search index (<c>search_code</c> / <c>tag_usage</c>)
/// expose CODE instead of raw &lt;Component&gt;/&lt;StlToken&gt; XML. STL — the common case — is
/// fully reconstructed; any other language, or a parse failure, returns the raw XML unchanged
/// (safe fallback). Ported from the working Python parser in
/// <c>Extract_PLC_Data_GUI/extract_plc_full.py</c> (<c>reconstruct_stl</c> / <c>_collect_stl_symbol</c>
/// / <c>extract_code_from_block</c>).
/// </summary>
internal static class BlockSourceReconstructor
{
    /// <summary>STL tokens that are NOT already their own mnemonic. Everything else in Openness
    /// StatementList XML serializes as its mnemonic verbatim (A / AN / O / L / T / S / R / ...),
    /// so it is emitted unchanged. Mirrors the Python <c>token_map</c> exactly.</summary>
    private static readonly Dictionary<string, string> TokenMap = new(StringComparer.Ordinal)
    {
        ["Assign"]   = "=",
        ["A_BRACK"]  = "A(",
        ["AN_BRACK"] = "AN(",
        ["O_BRACK"]  = "O(",
        ["ON_BRACK"] = "ON(",
        ["BRACKET"]  = ")",
        ["NOP_0"]    = "NOP 0",
        ["ADD_R"]    = "+R",
        ["SUB_R"]    = "-R",
        ["MUL_R"]    = "*R",
        ["DIV_R"]    = "/R",
        ["Rise"]     = "FP",
        ["Fall"]     = "FN",
        ["OnDelay"]  = "SD",
        ["OffDelay"] = "SF",
    };

    /// <summary>"--- FILE: name ---" separator lines <see cref="BlockExporter"/> prepends to a
    /// multi-file <c>ExportAsDocuments</c> result — strip them before parsing.</summary>
    private static readonly Regex FileSeparator =
        new(@"(?m)^---\s*FILE:.*?---\s*$", RegexOptions.Compiled);

    /// <summary>Splits a concatenated export (multiple <c>&lt;?xml ?&gt;</c> documents) into
    /// individually-parseable chunks.</summary>
    private static readonly Regex XmlDeclaration =
        new(@"<\?xml[^>]*\?>", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Reconstruct readable source for a block export. Only STL is reconstructed; any other
    /// programming language (or an unrecoverable parse failure) returns <paramref name="xml"/>
    /// unchanged so callers never lose data.
    /// </summary>
    public static string Reconstruct(string? xml, string? programmingLanguage)
    {
        if (string.IsNullOrEmpty(xml))
        {
            return string.Empty;
        }

        if (!IsStl(programmingLanguage))
        {
            return xml;
        }

        try
        {
            var compileUnits = CollectCompileUnits(xml);
            if (compileUnits.Count == 0)
            {
                return xml;
            }

            var sb = new StringBuilder();
            var network = 0;
            foreach (var compileUnit in compileUnits)
            {
                var statementList = FindStatementList(compileUnit);
                if (statementList == null)
                {
                    continue;
                }

                network++;
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }

                sb.Append("// Network ").Append(network).Append('\n');
                sb.Append(ReconstructStl(statementList));
            }

            return sb.Length == 0 ? xml : sb.ToString();
        }
        catch
        {
            return xml;
        }
    }

    /// <summary>Every <c>&lt;CompileUnit&gt;</c> / <c>&lt;SW.Blocks.CompileUnit&gt;</c> across the
    /// (possibly multi-document) export, each representing one network.</summary>
    private static List<XElement> CollectCompileUnits(string xml)
    {
        var result = new List<XElement>();
        var cleaned = FileSeparator.Replace(xml, string.Empty);
        foreach (var part in XmlDeclaration.Split(cleaned))
        {
            var trimmed = part.TrimStart();
            if (trimmed.Length == 0)
            {
                continue;
            }

            try
            {
                var root = XDocument.Parse(trimmed).Root;
                if (root != null)
                {
                    result.AddRange(root.Descendants().Where(IsCompileUnit));
                }
            }
            catch
            {
                /* skip an unparseable chunk; caller falls back to raw XML if nothing parses */
            }
        }

        return result;
    }

    /// <summary>Walk <c>CompileUnit &gt; AttributeList &gt; NetworkSource &gt; StatementList</c>
    /// and return the StatementList (the STL token stream) for one network.</summary>
    private static XElement? FindStatementList(XElement compileUnit)
    {
        var attributeList = compileUnit.Elements().FirstOrDefault(e => e.Name.LocalName == "AttributeList");
        var networkSource = attributeList?.Elements().FirstOrDefault(e => e.Name.LocalName == "NetworkSource");
        return networkSource?.Elements().FirstOrDefault(e => e.Name.LocalName == "StatementList");
    }

    private static string ReconstructStl(XElement statementList)
    {
        var sb = new StringBuilder();
        foreach (var stmt in statementList.Elements())
        {
            if (stmt.Name.LocalName != "StlStatement")
            {
                continue;
            }

            var tokenText = stmt.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "StlToken")?
                .Attribute("Text")?.Value ?? string.Empty;

            if (tokenText == "EMPTY_LINE")
            {
                sb.Append('\n');
                continue;
            }

            if (tokenText == "COMMENT")
            {
                foreach (var lineComment in stmt.Elements().Where(e => e.Name.LocalName == "LineComment"))
                {
                    foreach (var text in lineComment.Elements().Where(e => e.Name.LocalName == "Text"))
                    {
                        sb.Append("      //").Append(text.Value ?? string.Empty).Append('\n');
                    }
                }
                continue;
            }

            var mnemonic = MapToken(tokenText);
            if (mnemonic == ")")
            {
                sb.Append("      )\n");
                continue;
            }

            sb.Append("      ").Append(mnemonic).Append("     ");
            foreach (var child in stmt.Elements())
            {
                var localName = child.Name.LocalName;
                if (localName == "StlToken" || localName == "LineComment")
                {
                    continue;
                }

                if (localName == "Access")
                {
                    AppendOperand(sb, child);
                }
            }
            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static string MapToken(string tokenText)
        => TokenMap.TryGetValue(tokenText, out var mnemonic) ? mnemonic : tokenText;

    private static void AppendOperand(StringBuilder sb, XElement access)
    {
        switch (access.Attribute("Scope")?.Value ?? string.Empty)
        {
            case "GlobalVariable":
                foreach (var symbol in access.Elements().Where(e => e.Name.LocalName == "Symbol"))
                {
                    sb.Append(CollectSymbol(symbol));
                }
                break;

            case "LocalVariable":
                sb.Append('#');
                foreach (var symbol in access.Elements().Where(e => e.Name.LocalName == "Symbol"))
                {
                    sb.Append(CollectSymbol(symbol));
                }
                break;

            case "LiteralConstant":
            case "TypedConstant":
                foreach (var constant in access.Elements().Where(e => e.Name.LocalName == "Constant"))
                {
                    var value = constant.Elements().FirstOrDefault(e => e.Name.LocalName == "ConstantValue");
                    if (value != null)
                    {
                        sb.Append(value.Value ?? string.Empty);
                    }
                }
                break;

            case "Call":
                // STL call: <CallInfo Name="FC_LIJN" BlockType="FC"/> → "FC_LIJN".
                foreach (var callInfo in access.Elements().Where(e => e.Name.LocalName == "CallInfo"))
                {
                    var name = callInfo.Attribute("Name")?.Value;
                    if (!string.IsNullOrEmpty(name))
                    {
                        sb.Append('"').Append(name).Append('"');
                    }
                }
                break;
        }
    }

    /// <summary>Build the dotted symbol path for an operand. The first <c>Component</c>
    /// (DB/FC/FB) is always quoted in STL; later components quote only when HasQuotes=true.
    /// Ported from <c>_collect_stl_symbol</c>.</summary>
    private static string CollectSymbol(XElement symbol)
    {
        var segments = new List<string>();
        foreach (var child in symbol.Elements())
        {
            if (child.Name.LocalName != "Component")
            {
                continue;
            }

            var name = child.Attribute("Name")?.Value ?? string.Empty;
            string segment;
            if (segments.Count == 0)
            {
                segment = "\"" + name + "\"";
            }
            else
            {
                var hasQuotes = false;
                foreach (var attr in child.Elements().Where(e => e.Name.LocalName == "BooleanAttribute"))
                {
                    if ((attr.Attribute("Name")?.Value ?? string.Empty) == "HasQuotes")
                    {
                        hasQuotes = (attr.Value ?? string.Empty)
                            .Trim()
                            .Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                }
                segment = hasQuotes ? "\"" + name + "\"" : name;
            }

            segments.Add(segment);
        }

        return string.Join(".", segments);
    }

    private static bool IsCompileUnit(XElement e)
    {
        var localName = e.Name.LocalName;
        return localName == "CompileUnit" || localName == "SW.Blocks.CompileUnit";
    }

    private static bool IsStl(string? programmingLanguage)
        => !string.IsNullOrEmpty(programmingLanguage)
           && programmingLanguage.Equals("STL", StringComparison.OrdinalIgnoreCase);
}
