using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class BlockInterfaceReader
{
    private static readonly XNamespace InterfaceNs = "http://www.siemens.com/automation/Openness/SW/Interface/v5";

    public static BlockInterfaceInfo Read(Project project, string blockPath)
    {
        var address = BlockAddress.Parse(blockPath);
        var target = BlockTargetResolver.ResolveForExport(project, address);
        var block = target.Block
            ?? throw new InvalidOperationException($"Block '{blockPath}' not found.");

        var info = new BlockInterfaceInfo
        {
            BlockName = block.Name,
            BlockType = block switch
            {
                OB => "OB",
                FB => "FB",
                FC => "FC",
                GlobalDB => "GlobalDB",
                InstanceDB => "InstanceDB",
                _ => block.GetType().Name
            },
            BlockNumber = block.Number,
            ProgrammingLanguage = block.ProgrammingLanguage.ToString()
        };

        // Export to XML and parse interface
        string tempDir = Path.Combine(Path.GetTempPath(), "tia-mcp-iface-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Delete existing files before export (V21 requirement)
            foreach (var f in Directory.GetFiles(tempDir))
            {
                try { File.Delete(f); } catch { }
            }

#if LEGACY_TIA
            // V16-V18: Use legacy Export API (always XML)
            var exportPath = Path.Combine(tempDir, target.DocumentName);
            block.Export(new FileInfo(exportPath), ExportOptions.WithDefaults);
            var xml = XDocument.Load(exportPath);
            ParseInterfaceFromXml(xml, info);
#else
            var result = block.ExportAsDocuments(new DirectoryInfo(tempDir), target.DocumentName);

            if (result.State != DocumentResultState.Success)
            {
                throw new InvalidOperationException($"Block export failed with state: {result.State}");
            }

            var exportedFile = result.ExportedDocuments.FirstOrDefault()
                ?? throw new InvalidOperationException("Block export produced no documents.");

            // V16-V19 export blocks as XML; V21+ exports as .s7dcl (YAML).
            // Detect the format so DBs/FCs exported as .s7dcl still yield an interface
            // instead of crashing with "Data at the root level is invalid".
            var content = File.ReadAllText(exportedFile.FullName);
            if (content.TrimStart().StartsWith("<", StringComparison.Ordinal))
            {
                var xml = XDocument.Parse(content);
                ParseInterfaceFromXml(xml, info);
            }
            else
            {
                ParseInterfaceFromS7Dcl(content, info);
            }
#endif
        }
        catch (XmlException ex)
        {
            // XML parsing failed — return partial info with diagnostic instead of crashing
            info.Sections.Clear();
            info.DiagnosticMessage = $"XML parsing error for block '{info.BlockName}': {ex.Message}. " +
                                     "The block may use an unsupported format or contain corrupted data.";
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        return info;
    }

    private static void ParseInterfaceFromXml(XDocument xml, BlockInterfaceInfo info)
    {
        // Find the Interface element — it can be at different levels depending on block type
        var interfaceElement = xml.Descendants(InterfaceNs + "Interface").FirstOrDefault()
            ?? xml.Descendants("Interface").FirstOrDefault();

        if (interfaceElement is null)
        {
            // Some blocks (e.g., STL) might not have a structured interface in the exported XML
            return;
        }

        var sectionsElement = interfaceElement.Element(InterfaceNs + "Sections")
            ?? interfaceElement.Element("Sections");

        if (sectionsElement is null)
        {
            return;
        }

        foreach (var section in sectionsElement.Elements(InterfaceNs + "Section")
            .Concat(sectionsElement.Elements("Section")))
        {
            var sectionName = section.Attribute("Name")?.Value ?? "Unknown";

            var sectionInfo = new BlockSectionInfo
            {
                SectionName = sectionName
            };

            foreach (var member in section.Elements(InterfaceNs + "Member")
                .Concat(section.Elements("Member")))
            {
                var param = ParseMember(member);
                sectionInfo.Parameters.Add(param);
            }

            if (sectionInfo.Parameters.Count > 0 || sectionName is "Input" or "Output" or "InOut" or "Static" or "Temp" or "Return")
            {
                info.Sections.Add(sectionInfo);
            }
        }
    }

    // Matches a VAR member declaration like:  ACTUEEL_IN : Int;
    // or quoted names:  "DODE BAND" : Int;
    // The negative lookahead (?!=) avoids matching code assignments (Name := expr;).
    private static readonly Regex S7MemberRe = new(
        @"^\s*(?<name>""[^""]+""|[^\s:]+)\s*:(?!=)\s*(?<type>[^;{]+);",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, string> S7SectionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VAR_INPUT"] = "Input",
        ["VAR_OUTPUT"] = "Output",
        ["VAR_IN_OUT"] = "InOut",
        ["VAR_TEMP"] = "Temp",
        ["VAR_CONSTANT"] = "Constant",
        ["VAR_STATIC"] = "Static",
        ["VAR"] = "Static", // DB fields / FB static
    };

    /// <summary>
    /// Best-effort parser for the V21+ .s7dcl (SIMATIC SD YAML) interface format.
    /// Extracts VAR_* sections and their member name/type declarations. Code in
    /// NETWORK blocks and complex multi-line initializers are skipped.
    /// </summary>
    private static void ParseInterfaceFromS7Dcl(string content, BlockInterfaceInfo info)
    {
        BlockSectionInfo? currentSection = null;

        foreach (var rawLine in content.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            // Section start: VAR_INPUT, VAR_OUTPUT, VAR_TEMP, VAR (alone), etc.
            if (line.StartsWith("VAR", StringComparison.OrdinalIgnoreCase))
            {
                var keyword = line.Split(' ', '(')[0];
                if (S7SectionMap.TryGetValue(keyword, out var sectionName))
                {
                    currentSection = new BlockSectionInfo { SectionName = sectionName };
                }

                continue;
            }

            if (line.Equals("END_VAR", StringComparison.OrdinalIgnoreCase))
            {
                if (currentSection is not null &&
                    (currentSection.Parameters.Count > 0 ||
                     IsStandardSection(currentSection.SectionName)))
                {
                    info.Sections.Add(currentSection);
                }

                currentSection = null;
                continue;
            }

            // Member declarations only appear inside a VAR section
            if (currentSection is null)
            {
                continue;
            }

            var match = S7MemberRe.Match(rawLine);
            if (match.Success)
            {
                currentSection.Parameters.Add(new BlockParameterInfo
                {
                    Name = match.Groups["name"].Value.Trim().Trim('"'),
                    DataType = match.Groups["type"].Value.Trim(),
                });
            }
        }
    }

    private static bool IsStandardSection(string sectionName) =>
        sectionName is "Input" or "Output" or "InOut" or "Static" or "Temp" or "Constant" or "Return";

    private static BlockParameterInfo ParseMember(XElement member)
    {
        var param = new BlockParameterInfo
        {
            Name = member.Attribute("Name")?.Value ?? "",
            DataType = member.Attribute("Datatype")?.Value
                ?? member.Attribute("DataType")?.Value
                ?? ""
        };

        // StartValue
        var startValueElement = member.Element(InterfaceNs + "StartValue")
            ?? member.Element("StartValue");
        if (startValueElement is not null)
        {
            param.StartValue = startValueElement.Value;
        }
        else
        {
            // StartValue can be an attribute on some block types
            var svAttr = member.Attribute("StartValue");
            if (svAttr is not null)
            {
                param.StartValue = svAttr.Value;
            }
        }

        // Comment
        var commentElement = member.Element(InterfaceNs + "Comment")
            ?? member.Element("Comment");
        if (commentElement is not null)
        {
            // Prefer en-US, fall back to first available
            var multiTexts = commentElement.Elements(InterfaceNs + "MultiLanguageText")
                .Concat(commentElement.Elements("MultiLanguageText"));

            var enText = multiTexts.FirstOrDefault(e =>
                string.Equals(e.Attribute("Lang")?.Value, "en-US", StringComparison.OrdinalIgnoreCase));

            param.Comment = (enText ?? multiTexts.FirstOrDefault())?.Attribute("Text")?.Value;
        }

        // Accessibility
        var attrAccessible = member.Attribute("Accessible");
        if (attrAccessible is not null && bool.TryParse(attrAccessible.Value, out var accessible))
        {
            param.accessible = accessible;
        }

        // Visibility
        var attrVisible = member.Attribute("Visible");
        if (attrVisible is not null && bool.TryParse(attrVisible.Value, out var visible))
        {
            param.visible = visible;
        }

        // Remanence
        var attrRemanence = member.Attribute("Remanence");
        if (attrRemanence is not null)
        {
            param.Remanence = attrRemanence.Value;
        }

        // Offset
        var attrOffset = member.Attribute("Offset");
        if (attrOffset is not null && int.TryParse(attrOffset.Value, out var offset))
        {
            param.Offset = offset;
        }

        return param;
    }
}
