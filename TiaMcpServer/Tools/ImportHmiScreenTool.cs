using System;
using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using TiaMcpServer.Safety;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools;

[McpServerToolType]
public static class ImportHmiScreenTool
{
    [McpServerTool(Name = "preview_import_hmi_screen")]
    [Description("Preview importing an HMI screen and return a short-lived safetyToken. The preview shows the diff between the current screen (if it exists) and the new XML content. Pass the token to import_hmi_screen after reviewing the preview.")]
    public static async Task<string> PreviewImportHmiScreen(
        OpennessWorkerClient workerClient,
        [Description("Name of the HMI device to import the screen into (e.g. 'HMI_1').")] string deviceName,
        [Description("Name for the imported screen (e.g. 'NEW_SCREEN'). If a screen with this name exists, it will be replaced.")] string screenName,
        [Description("XML content of the HMI screen to import. Must be valid TIA Portal HMI screen XML format.")] string xmlContent,
        [Description("Optional screen folder path within the HMI device. If omitted, imports to the root Screens folder.")] string? folderPath = null,
        [Description("Optional project path.")] string? projectPath = null,
        [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
    {
        var currentState = await workerClient.ExportHmiScreenAsync(deviceName, screenName, projectPath, tiaVersion).ConfigureAwait(false);
        var target = new { deviceName, screenName, folderPath };
        var requestedInput = new { deviceName, screenName, folderPath, xmlContent };

        return WriteSafetyTooling.CreatePreview(
            "import_hmi_screen",
            projectPath,
            target,
            $"Import HMI screen '{screenName}' into device '{deviceName}'.",
            requestedInput,
            currentState,
            WriteSafetyTooling.CreateLineDiff(currentState, xmlContent));
    }

    [McpServerTool(Name = "import_hmi_screen")]
    [Description("Import an HMI screen from XML into the TIA Portal project. Requires confirm=true and a safetyToken from preview_import_hmi_screen.")]
    public static async Task<string> ImportHmiScreen(
        OpennessWorkerClient workerClient,
        [Description("Name of the HMI device to import the screen into (e.g. 'HMI_1').")] string deviceName,
        [Description("Name for the imported screen (e.g. 'NEW_SCREEN'). If a screen with this name exists, it will be replaced.")] string screenName,
        [Description("XML content of the HMI screen to import. Must be valid TIA Portal HMI screen XML format.")] string xmlContent,
        [Description("Set to true to confirm the write operation. Required safety flag — operation is rejected when false.")] bool confirm = false,
        [Description("Safety token returned by preview_import_hmi_screen for this exact write request.")] string? safetyToken = null,
        [Description("Optional screen folder path within the HMI device. If omitted, imports to the root Screens folder.")] string? folderPath = null,
        [Description("Optional project path.")] string? projectPath = null,
        [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
    {
        if (!confirm)
        {
            return "Operation not confirmed. Set confirm=true to proceed with the HMI screen import.";
        }

        var target = new { deviceName, screenName, folderPath };
        var requestedInput = new { deviceName, screenName, folderPath, xmlContent };

        var safety = await WriteSafetyTooling.ValidateForApplyAsync(
            safetyToken,
            "preview_import_hmi_screen",
            "import_hmi_screen",
            projectPath,
            target,
            requestedInput,
            () => workerClient.ExportHmiScreenAsync(deviceName, screenName, projectPath, tiaVersion)).ConfigureAwait(false);

        if (!safety.IsValid)
        {
            return safety.Error!;
        }

        var result = await workerClient.ImportHmiScreenAsync(deviceName, screenName, folderPath, xmlContent, projectPath, tiaVersion).ConfigureAwait(false);

        WriteSafetyService.Shared.AppendAudit(
            "import_hmi_screen",
            projectPath,
            target,
            requestedInput,
            safety.CurrentState,
            result);

        return result;
    }
}
