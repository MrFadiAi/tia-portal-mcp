using System.Collections;
using System.Reflection;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Imports XML content into an HMI screen in TIA Portal via the Openness API.
/// Uses reflection because HMI types vary by TIA Portal version.
/// </summary>
public static class HmiScreenImporter
{
    private const string FileSeparatorPrefix = "--- FILE:";

    public static string Import(Project project, string deviceName, string screenName, string? folderPath, string xmlContent)
    {
        if (project is null) throw new ArgumentNullException(nameof(project));
        if (deviceName is null) throw new ArgumentNullException(nameof(deviceName));
        if (screenName is null) throw new ArgumentNullException(nameof(screenName));
        if (xmlContent is null) throw new ArgumentNullException(nameof(xmlContent));

        // Find the target screen container via reflection
        var target = FindScreenContainer(project, deviceName, folderPath);

        string tempDir = Path.Combine(Path.GetTempPath(), "tia-mcp-hmi-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            WriteContentToTempDir(tempDir, screenName, xmlContent);

#if LEGACY_TIA
            // V16-V18: Use legacy Import API (single file XML import)
            var importFile = Directory.GetFiles(tempDir).FirstOrDefault()
                ?? throw new InvalidOperationException("No file was written to temp directory.");

            var importMethod = target.ContainerType.GetMethod("Import", new[] { typeof(FileInfo), typeof(ImportOptions) })
                ?? throw new InvalidOperationException(
                    $"Screen container type '{target.ContainerType.Name}' does not have an Import(FileInfo, ImportOptions) method.");

            var fileInfo = new FileInfo(importFile);
            importMethod.Invoke(target.Container, new object[] { fileInfo, ImportOptions.Override });

            return $"Import succeeded: screen '{screenName}' imported into device '{deviceName}'.";
#else
            // V21+: Use ImportFromDocuments API (directory-based import)
            // Resolve ImportDocumentOptions.Override via reflection (type varies by TIA version)
            var importDocOptionsType = target.ContainerType.Assembly.GetType("Siemens.Engineering.ImportDocumentOptions")
                ?? throw new InvalidOperationException("Could not resolve ImportDocumentOptions type.");
            var overrideValue = Enum.Parse(importDocOptionsType, "Override");

            var importMethod = target.ContainerType.GetMethod("ImportFromDocuments", new[] { typeof(DirectoryInfo), typeof(string), importDocOptionsType })
                ?? throw new InvalidOperationException(
                    $"Screen container type '{target.ContainerType.Name}' does not have an ImportFromDocuments method.");

            var result = importMethod.Invoke(target.Container, new object[] { new DirectoryInfo(tempDir), screenName, overrideValue });

            // Check DocumentResult if available
            if (result is not null)
            {
                var stateProp = result.GetType().GetProperty("State");
                if (stateProp is not null)
                {
                    var state = stateProp.GetValue(result);
                    if (state is not null && state.ToString() != "Success")
                    {
                        throw new InvalidOperationException($"Import failed with state: {state}");
                    }
                }
            }

            return $"Import succeeded: screen '{screenName}' imported into device '{deviceName}'.";
#endif
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException ?? ex;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private static (object Container, Type ContainerType) FindScreenContainer(Project project, string deviceName, string? folderPath)
    {
        foreach (Device device in project.Devices)
        {
            if (!string.Equals(device.Name, deviceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var container = FindDeviceScreenContainer(device, folderPath);
                if (container is not null)
                {
                    return container.Value;
                }
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Error finding screen container in device '{device.Name}': {ex.Message}");
            }
        }

        throw new InvalidOperationException(
            $"Could not find HMI screen container for device '{deviceName}'" +
            (folderPath is not null ? $" at folder path '{folderPath}'." : ".") +
            " Ensure the device exists, contains HMI software, and the folder path is correct.");
    }

    private static (object Container, Type ContainerType)? FindDeviceScreenContainer(Device device, string? folderPath)
    {
        foreach (DeviceItem item in device.DeviceItems)
        {
            try
            {
                var swContainer = item.GetService<SoftwareContainer>();
                if (swContainer?.Software is null)
                {
                    continue;
                }

                var software = swContainer.Software;
                var softwareType = software.GetType();

                if (!IsHmiSoftware(softwareType))
                {
                    continue;
                }

                // Try to find the Screens collection directly on the software object
                var screensCollection = ReadPropertySafe(software, "Screens");
                if (screensCollection is not null)
                {
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        return (screensCollection, screensCollection.GetType());
                    }

                    // Walk folder tree to find the target folder
                    var targetFolder = FindScreenFolder(screensCollection, folderPath);
                    if (targetFolder is not null)
                    {
                        return (targetFolder, targetFolder.GetType());
                    }

                    // Folder not found; fall through — the root Screens collection is the fallback
                    return (screensCollection, screensCollection.GetType());
                }

                // Try ScreenFolder or Folders as the root container
                var screenFolder = ReadPropertySafe(software, "ScreenFolder")
                                   ?? ReadPropertySafe(software, "Folders");

                if (screenFolder is not null)
                {
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        // Try to get Screens from the root folder
                        var rootScreens = ReadPropertySafe(screenFolder, "Screens")
                                          ?? ReadPropertySafe(screenFolder, "ScreenItems");

                        if (rootScreens is not null)
                        {
                            return (rootScreens, rootScreens.GetType());
                        }

                        // The folder itself may support Import
                        return (screenFolder, screenFolder.GetType());
                    }

                    // Walk folder tree to find the target folder
                    var targetFolder = FindScreenFolder(screenFolder, folderPath);
                    if (targetFolder is not null)
                    {
                        var folderScreens = ReadPropertySafe(targetFolder, "Screens")
                                            ?? ReadPropertySafe(targetFolder, "ScreenItems");

                        if (folderScreens is not null)
                        {
                            return (folderScreens, folderScreens.GetType());
                        }

                        // The folder itself may support Import
                        return (targetFolder, targetFolder.GetType());
                    }
                }
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping device item while finding HMI screen container in device '{device.Name}': {ex.Message}");
            }
        }

        return null;
    }

    private static object? FindScreenFolder(object rootContainer, string folderPath)
    {
        // Split the folder path into segments and walk the tree
        var segments = folderPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        // Start at the root container's Folders collection
        var currentFolders = ReadPropertySafe(rootContainer, "Folders")
                             ?? ReadPropertySafe(rootContainer, "Subfolders");

        if (currentFolders is null)
        {
            return null;
        }

        object? currentFolder = null;

        foreach (var segment in segments)
        {
            var found = false;

            foreach (var folder in EnumerateSafe(currentFolders, $"folders at segment '{segment}'"))
            {
                var folderName = ReadPropertySafe(folder, "Name")?.ToString();
                if (string.Equals(folderName, segment, StringComparison.OrdinalIgnoreCase))
                {
                    currentFolder = folder;
                    currentFolders = ReadPropertySafe(folder, "Folders")
                                     ?? ReadPropertySafe(folder, "Subfolders");
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return null;
            }
        }

        return currentFolder;
    }

    private static void WriteContentToTempDir(string tempDir, string screenName, string xmlContent)
    {
        if (!xmlContent.Contains(FileSeparatorPrefix))
        {
            File.WriteAllText(Path.Combine(tempDir, screenName), xmlContent);
            return;
        }

        string[] lines = xmlContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        string? currentFileName = null;
        var sectionLines = new System.Collections.Generic.List<string>();

        foreach (string line in lines)
        {
            if (line.StartsWith(FileSeparatorPrefix, StringComparison.Ordinal))
            {
                FlushSection(tempDir, currentFileName, sectionLines);
                currentFileName = ExtractFileName(line);
                sectionLines.Clear();
            }
            else
            {
                sectionLines.Add(line);
            }
        }

        FlushSection(tempDir, currentFileName, sectionLines);
    }

    private static string ExtractFileName(string separatorLine)
    {
        // Expected format: "--- FILE: filename ---"
        string inner = separatorLine.Substring(FileSeparatorPrefix.Length).TrimEnd();
        if (inner.EndsWith("---", StringComparison.Ordinal))
        {
            inner = inner.Substring(0, inner.Length - 3);
        }

        return inner.Trim();
    }

    private static void FlushSection(
        string tempDir,
        string? fileName,
        System.Collections.Generic.List<string> lines)
    {
        if (fileName is null || lines.Count == 0)
        {
            return;
        }

        string content = string.Join(Environment.NewLine, lines);
        File.WriteAllText(Path.Combine(tempDir, fileName), content);
    }

    // --- Reflection helpers (same pattern as HmiScreenReader) ---

    private static bool IsHmiSoftware(Type type)
    {
        var name = type.Name;
        return name.Contains("HmiSoftware") ||
               name.Contains("HmiTarget") ||
               name.Contains("UnifiedHmiSoftware") ||
               name.Contains("ScreenProvider");
    }

    private static object? ReadPropertySafe(object instance, string propertyName)
    {
        try
        {
            return instance.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(instance);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is EngineeringException)
        {
            Console.Error.WriteLine($"Skipping property '{propertyName}': {ex.InnerException!.Message}");
            return null;
        }
        catch (EngineeringException ex)
        {
            Console.Error.WriteLine($"Skipping property '{propertyName}': {ex.Message}");
            return null;
        }
    }

    private static IEnumerable<object> EnumerateSafe(object? enumerable, string description)
    {
        if (enumerable is null) yield break;
        if (enumerable is not IEnumerable ie) yield break;

        IEnumerator enumerator;
        try
        {
            enumerator = ie.GetEnumerator();
        }
        catch (EngineeringException ex)
        {
            Console.Error.WriteLine($"Skipping {description}: {ex.Message}");
            yield break;
        }

        while (true)
        {
            object? current;
            try
            {
                if (!enumerator.MoveNext()) yield break;
                current = enumerator.Current;
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping an entry in {description}: {ex.Message}");
                continue;
            }

            if (current is not null)
            {
                yield return current;
            }
        }
    }
}
