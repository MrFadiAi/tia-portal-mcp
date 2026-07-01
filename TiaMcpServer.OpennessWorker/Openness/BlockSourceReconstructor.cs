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
    /// Reconstruct readable source for a block export. STL and SCL are reconstructed; any other
    /// programming language (or an unrecoverable parse failure) returns <paramref name="xml"/>
    /// unchanged so callers never lose data.
    /// </summary>
    public static string Reconstruct(string? xml, string? programmingLanguage)
    {
        if (string.IsNullOrEmpty(xml))
        {
            return string.Empty;
        }

        var isStl = IsStl(programmingLanguage);
        var isScl = IsScl(programmingLanguage);
        if (!isStl && !isScl)
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
                var languageRoot = isStl ? FindStatementList(compileUnit) : FindStructuredText(compileUnit);
                if (languageRoot == null)
                {
                    continue;
                }

                network++;
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }

                sb.Append("// Network ").Append(network).Append('\n');
                sb.Append(isStl ? ReconstructStl(languageRoot) : ReconstructScl(languageRoot));
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

    /// <summary>As <see cref="FindStatementList"/> but for SCL: returns the
    /// <c>StructuredText</c> element under <c>NetworkSource</c>.</summary>
    private static XElement? FindStructuredText(XElement compileUnit)
    {
        var attributeList = compileUnit.Elements().FirstOrDefault(e => e.Name.LocalName == "AttributeList");
        var networkSource = attributeList?.Elements().FirstOrDefault(e => e.Name.LocalName == "NetworkSource");
        return networkSource?.Elements().FirstOrDefault(e => e.Name.LocalName == "StructuredText");
    }

    /// <summary>
    /// Reconstruct readable SCL from a <c>StructuredText</c> element. SCL XML is a flat stream of
    /// layout tokens (Token/Blank/Text/NewLine) plus operands (Access). The common operands reuse
    /// <see cref="AppendOperand"/> (GlobalVariable/LocalVariable/Constant). Ported from the Python
    /// <c>_append_scl_part</c>; rare constructs (call parameter lists, absolute addresses) are
    /// skipped gracefully rather than crashing.
    /// </summary>
    private static string ReconstructScl(XElement structuredText)
    {
        var sb = new StringBuilder();
        foreach (var child in structuredText.Elements())
        {
            AppendSclPart(sb, child);
        }

        return sb.ToString();
    }

    private static void AppendSclPart(StringBuilder sb, XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "Token":
            case "NamePart":
                AppendAttribute(sb, element, "Text");
                break;

            case "Blank":
                sb.Append(' ', ParseIntAttribute(element, "Num", 1));
                break;

            case "Text":
                sb.Append(element.Value ?? string.Empty);
                break;

            case "NewLine":
                sb.Append('\n', ParseIntAttribute(element, "Num", 1));
                break;

            case "Date":
            case "Time":
                AppendAttribute(sb, element, "Value");
                break;

            case "LineComment":
                AppendCommentText(sb, element, "//");
                break;

            case "BlockComment":
                sb.Append("(*");
                foreach (var sub in element.Elements())
                {
                    if (sub.Name.LocalName == "Text")
                    {
                        sb.Append(sub.Value ?? string.Empty);
                    }
                    else if (sub.Name.LocalName == "NewLine")
                    {
                        sb.Append('\n');
                    }
                }

                sb.Append("*)");
                break;

            case "Access":
                // Reuses the STL operand handler: GlobalVariable -> "Tag", LocalVariable -> #name,
                // Literal/TypedConstant -> value, Call -> "Block". SCL-only scopes
                // (Address/Label/LocalConstant/Input/...) are not handled here and are skipped.
                AppendOperand(sb, element);
                break;
        }
    }

    private static void AppendAttribute(StringBuilder sb, XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (!string.IsNullOrEmpty(value))
        {
            sb.Append(value);
        }
    }

    private static int ParseIntAttribute(XElement element, string attributeName, int defaultValue)
        => int.TryParse(element.Attribute(attributeName)?.Value, out var n) ? n : defaultValue;

    /// <summary>Append a line/block comment's text. SCL comments carry their text in
    /// <c>&lt;Text&gt;</c> children (or directly). <paramref name="prefix"/> is <c>//</c> or empty.</summary>
    private static void AppendCommentText(StringBuilder sb, XElement element, string prefix)
    {
        var hasTextChild = false;
        foreach (var text in element.Elements().Where(e => e.Name.LocalName == "Text"))
        {
            hasTextChild = true;
            sb.Append(prefix).Append(text.Value ?? string.Empty);
        }

        if (!hasTextChild && !string.IsNullOrEmpty(element.Value))
        {
            sb.Append(prefix).Append(element.Value);
        }
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
                // Locals are '#name' (never quoted, unlike globals '"Name"').
                foreach (var symbol in access.Elements().Where(e => e.Name.LocalName == "Symbol"))
                {
                    sb.Append(CollectSymbol(symbol, quoteFirst: false));
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
                AppendCall(sb, access);
                break;
        }
    }

    /// <summary>Reconstruct a block call: "NAME" followed by one "PARAM := value"
    /// line per parameter. Real TIA Openness stores each &lt;Parameter&gt; with only
    /// the value &lt;Access&gt; (no ':=' token, no newline), so the reconstructor
    /// renders the separator and line breaks itself. Without this the call rendered
    /// as a bare "NAME" with every parameter dropped — which made the AI report
    /// "call has NO parameters" (a false finding). Handles STL block calls and the
    /// SCL Access=Call path (same shape).</summary>
    private static void AppendCall(StringBuilder sb, XElement access)
    {
        foreach (var info in access.Elements().Where(e => e.Name.LocalName == "CallInfo" || e.Name.LocalName == "Instruction"))
        {
            var name = info.Attribute("Name")?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                sb.Append('"').Append(name).Append('"');
            }

            foreach (var parameter in info.Elements().Where(e => e.Name.LocalName == "Parameter"))
            {
                var pname = parameter.Attribute("Name")?.Value ?? string.Empty;
                sb.Append('\n').Append("      ").Append(pname).Append(" := ");
                foreach (var child in parameter.Elements())
                {
                    if (child.Name.LocalName == "Access")
                    {
                        AppendOperand(sb, child);
                    }
                }
                // Unconnected parameter (no <Access>) leaves a bare " := " — rare,
                // and still better than silently dropping it.
            }
        }
    }

    /// <summary>Build the dotted symbol path for an operand. The first <c>Component</c>
    /// (DB/FC/FB) is quoted for GLOBAL symbols (<c>"Name"</c>) but left unquoted for LOCAL
    /// symbols (<c>#name</c>); later components quote only when HasQuotes=true. Array indexes
    /// (<c>data[1]</c>, <c>data[#i]</c>) are reconstructed from the indexed Component.</summary>
    private static string CollectSymbol(XElement symbol, bool quoteFirst = true)
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
                segment = quoteFirst ? "\"" + name + "\"" : name;
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

            // Array index: current TIA encodes it as Component AccessModifier="Array"
            // with a child <Access>; older TIA uses Token "[" + Access + Token "]"
            // inside the Component. Without this the index is silently dropped
            // (data[1] -> data), which produced false "all registers collide" findings.
            var index = CollectArrayIndex(child);
            if (!string.IsNullOrEmpty(index))
            {
                segment += "[" + index + "]";
            }

            segments.Add(segment);
        }

        return string.Join(".", segments);
    }

    /// <summary>Extract an array index from a Component's children. Handles both the
    /// current TIA form (<c>Component AccessModifier="Array"</c> with a child
    /// <c>&lt;Access&gt;</c>) and the older <c>Token "[" + Access + Token "]"</c> form.
    /// Ported from the Python <c>_collect_array_index</c>.</summary>
    private static string CollectArrayIndex(XElement component)
    {
        // Current form: <Component Name="x" AccessModifier="Array"><Access>idx</Access></Component>
        if ((component.Attribute("AccessModifier")?.Value ?? string.Empty) == "Array")
        {
            var sb = new StringBuilder();
            foreach (var access in component.Elements().Where(e => e.Name.LocalName == "Access"))
            {
                AppendOperand(sb, access);
            }

            return sb.ToString();
        }

        // Older form: Token "[" + Access + Token "]" as children of the Component.
        var hasOpenBracket = component.Elements()
            .Any(e => e.Name.LocalName == "Token" && (e.Attribute("Text")?.Value ?? string.Empty) == "[");
        if (!hasOpenBracket)
        {
            return string.Empty;
        }

        var idx = new StringBuilder();
        var inBracket = false;
        foreach (var c in component.Elements())
        {
            var tag = c.Name.LocalName;
            if (tag == "Token")
            {
                var text = c.Attribute("Text")?.Value ?? string.Empty;
                if (text == "[")
                {
                    inBracket = true;
                    continue;
                }

                if (text == "]")
                {
                    break; // closes the index; exits the foreach
                }

                if (inBracket)
                {
                    idx.Append(text);
                }
            }
            else if (tag == "Access" && inBracket)
            {
                AppendOperand(idx, c);
            }
        }

        return idx.ToString();
    }

    private static bool IsCompileUnit(XElement e)
    {
        var localName = e.Name.LocalName;
        return localName == "CompileUnit" || localName == "SW.Blocks.CompileUnit";
    }

    private static bool IsStl(string? programmingLanguage)
        => !string.IsNullOrEmpty(programmingLanguage)
           && programmingLanguage.Equals("STL", StringComparison.OrdinalIgnoreCase);

    private static bool IsScl(string? programmingLanguage)
        => !string.IsNullOrEmpty(programmingLanguage)
           && programmingLanguage.Equals("SCL", StringComparison.OrdinalIgnoreCase);
}
