using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    /// <summary>
    /// PLC type (UDT) content tools. get_type_content is the READABLE read (reconstructed
    /// interface listing); update_type_content is the XML-only, update-only WRITE with a
    /// confirm gate.
    /// </summary>
    [McpServerToolType]
    public static class PlcTypeTools
    {
        [McpServerTool(Name = "get_type_content")]
        [Description(
            "Read one PLC type (UDT/PlcStruct) as a READABLE interface listing (members, data types, " +
            "start values — same reconstruction as block reads, derived from the export XML so it is " +
            "timestamp-free and stable). Use list_plc_types to enumerate types and export_plc_type when " +
            "you need the canonical raw XML (e.g. to prepare an update).")]
        public static async Task<string> GetTypeContent(
            OpennessWorkerClient workerClient,
            [Description("Type name, e.g. 'AnalogInputSettings' (from list_plc_types).")] string typeName,
            [Description("Optional PLC name to disambiguate when the project has several PLCs.")] string? plcName = null,
            [Description("Optional type-folder path when the type lives in a subfolder (from list_plc_types).")] string? folderPath = null,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.GetTypeContentAsync(typeName, plcName, folderPath, projectPath, tiaVersion).ConfigureAwait(false);
        }

        [McpServerTool(Name = "update_type_content")]
        [Description(
            "Update an EXISTING PLC type (UDT) from a TIA type export XML. UPDATE-ONLY — two guards " +
            "refuse before anything is written: (1) the type must already exist (create types in TIA " +
            "Portal), (2) the XML's declared <Name> must equal typeName (prevents accidentally creating " +
            "a different type). Workflow: export_plc_type -> edit the XML -> pass it here with " +
            "confirm=true. Blocks that use the type need recompiling afterward (compile_check).")]
        public static async Task<string> UpdateTypeContent(
            OpennessWorkerClient workerClient,
            [Description("Type name of the EXISTING type to update, e.g. 'AnalogInputSettings'.")] string typeName,
            [Description("The complete TIA type export XML (as returned by export_plc_type), with edits applied.")] string xmlContent,
            [Description("Set to true to confirm the update. Required safety flag — operation is rejected when false.")] bool confirm = false,
            [Description("Optional PLC name to disambiguate when the project has several PLCs.")] string? plcName = null,
            [Description("Optional type-folder path when the type lives in a subfolder (from list_plc_types).")] string? folderPath = null,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with the type update (the type's XML will be re-imported with Override).";
            }

            return await workerClient.UpdateTypeContentAsync(typeName, plcName, folderPath, xmlContent, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
