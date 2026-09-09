using System;
using System.Xml;
using System.Xml.Linq;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Pure preflight for update_type_content: reads the type name a PLC-type export XML DECLARES.
/// In the real V21 export shape the object name is the first non-empty <c>&lt;Name&gt;</c>
/// ELEMENT inside <c>AttributeList</c> (the <c>Interface</c> tree only carries <c>Name</c>
/// ATTRIBUTES on Section/Member nodes, so they never collide). Siemens-free.
/// </summary>
public static class PlcTypeNamePreflight
{
    public static bool TryReadDeclaredXmlName(string? xml, out string? declaredName, out string? error)
    {
        declaredName = null;
        error = null;

        if (string.IsNullOrWhiteSpace(xml))
        {
            error = "Type XML content is required.";
            return false;
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            error = $"Type XML is not well-formed: {ex.Message}";
            return false;
        }

        foreach (var element in document.Descendants())
        {
            if (!string.Equals(element.Name.LocalName, "Name", StringComparison.Ordinal))
            {
                continue;
            }

            var value = element.Value.Trim();
            if (value.Length > 0)
            {
                declaredName = value;
                return true;
            }
        }

        error =
            "No <Name> element with a non-empty value was found in the type XML — the declared " +
            "type name cannot be verified. Pass a genuine TIA type export (see export_plc_type).";
        return false;
    }
}
