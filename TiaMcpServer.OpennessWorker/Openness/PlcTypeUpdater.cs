using System;
using System.IO;
using Siemens.Engineering;
using Siemens.Engineering.SW.Types;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// PLC type (UDT) update via the XML import route, mirroring the upstream PlcTypeImporter XML
/// path: write the XML to a temp file and <c>Types.Import(FileInfo, ImportOptions.Override)</c>
/// into the type's own group. Update-only by design — both guards (type must exist; the XML's
/// declared &lt;Name&gt; must match the request) refuse before anything is written, so a wrong
/// XML can never create a stray type. Creating brand-new types stays a TIA Portal action.
/// </summary>
public static class PlcTypeUpdater
{
    public static PlcTypeUpdateResultInfo Update(
        Project project, string typeName, string? plcName, string? folderPath, string xml)
    {
        if (!PlcTypeNamePreflight.TryReadDeclaredXmlName(xml, out var declaredName, out var preflightError))
        {
            throw new InvalidOperationException(preflightError);
        }

        if (!string.Equals(declaredName, typeName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The XML declares type '{declaredName}' but the request targets '{typeName}'. " +
                "update_type_content is UPDATE-ONLY: pass XML whose declared <Name> matches the target " +
                "(this guard prevents accidentally creating a different type).");
        }

        var (type, group) = PlcTypeExporter.Locate(project, typeName, plcName, folderPath);
        if (type is null)
        {
            throw new InvalidOperationException(
                $"PLC type '{typeName}' was not found. update_type_content is UPDATE-ONLY: create the " +
                "type in TIA Portal first, then update it here.");
        }

        string tempFile = Path.Combine(Path.GetTempPath(), "tia-mcp-type-update-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            File.WriteAllText(tempFile, xml);

            // Same XML import route for every TIA flavor (the legacy Import call is still the
            // supported type-import API in V21; upstream uses it unconditionally for types).
            group.Types.Import(new FileInfo(tempFile), ImportOptions.Override);
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }

        return new PlcTypeUpdateResultInfo
        {
            Applied = true,
            TypeName = typeName,
            DeclaredName = declaredName,
            Message =
                $"Type '{typeName}' was updated from XML (ImportOptions.Override). Compile the PLC " +
                "(compile_check) to validate the change."
        };
    }
}
