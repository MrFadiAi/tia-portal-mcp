using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            // V16-V18: Use legacy Export API
            var exportPath = Path.Combine(tempDir, target.DocumentName);
            block.Export(new FileInfo(exportPath), ExportOptions.WithDefaults);
            var xml = XDocument.Load(exportPath);
#else
            var result = block.ExportAsDocuments(new DirectoryInfo(tempDir), target.DocumentName);

            if (result.State != DocumentResultState.Success)
            {
                throw new InvalidOperationException($"Block export failed with state: {result.State}");
            }

            var exportedFile = result.ExportedDocuments.FirstOrDefault()
                ?? throw new InvalidOperationException("Block export produced no documents.");

            var xml = XDocument.Load(exportedFile.FullName);
#endif
            ParseInterfaceFromXml(xml, info);
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
