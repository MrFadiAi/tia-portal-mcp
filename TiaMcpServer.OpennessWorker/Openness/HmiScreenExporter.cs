using System.Linq;
using System.Reflection;
using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Exports an HMI screen from TIA Portal to XML using reflection.
/// HMI types vary by TIA version, so all access is reflection-based.
/// </summary>
public static class HmiScreenExporter
{
    public static string Export(Project project, string deviceName, string screenName)
    {
        object? screenObj = null;
        List<string> availableScreens = new();

        foreach (Device device in project.Devices)
        {
            if (!string.Equals(device.Name, deviceName, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var (found, discovered) = FindScreenInDevice(device, screenName);
                availableScreens.AddRange(discovered);
                if (found is not null)
                {
                    screenObj = found;
                    break;
                }
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Error searching device '{device.Name}' for HMI screen: {ex.Message}");
            }
        }

        if (screenObj is null)
        {
            var message = availableScreens.Count > 0
                ? $"HMI screen '{screenName}' not found on device '{deviceName}'. Available screens: {string.Join(", ", availableScreens)}"
                : $"No HMI screens found on device '{deviceName}'. The device may not contain HMI software.";
            throw new InvalidOperationException(message);
        }

        return ExportScreenToXml(screenObj, screenName);
    }

    private static (object? Screen, List<string> Available) FindScreenInDevice(Device device, string screenName)
    {
        var available = new List<string>();

        foreach (DeviceItem item in device.DeviceItems)
        {
            try
            {
                var container = item.GetService<SoftwareContainer>();
                if (container?.Software is null)
                    continue;

                var software = container.Software;
                if (!IsHmiSoftware(software.GetType()))
                    continue;

                var (screen, names) = SearchScreensInSoftware(software, screenName);
                available.AddRange(names);
                if (screen is not null)
                    return (screen, available);
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping device item while searching HMI on device '{device.Name}': {ex.Message}");
            }
        }

        return (null, available);
    }

    private static bool IsHmiSoftware(Type type)
    {
        var name = type.Name;
        return name.Contains("HmiSoftware") ||
               name.Contains("HmiTarget") ||
               name.Contains("UnifiedHmiSoftware") ||
               name.Contains("ScreenProvider");
    }

    private static (object? Screen, List<string> Names) SearchScreensInSoftware(object software, string screenName)
    {
        var names = new List<string>();

        // Try Screens collection first
        var screensCollection = ReadPropertySafe(software, "Screens");
        if (screensCollection is not null)
        {
            foreach (var screenObj in EnumerateSafe(screensCollection, "HMI screens"))
            {
                try
                {
                    var name = ReadPropertySafe(screenObj, "Name")?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        names.Add(name!);
                        if (string.Equals(name, screenName, StringComparison.OrdinalIgnoreCase))
                            return (screenObj, names);
                    }
                }
                catch (EngineeringException ex)
                {
                    Console.Error.WriteLine($"Skipping HMI screen during search: {ex.Message}");
                }
            }
        }

        // Walk screen folders
        var screenFolder = ReadPropertySafe(software, "ScreenFolder") ??
                           ReadPropertySafe(software, "Folders");

        if (screenFolder is not null)
        {
            var (found, folderNames) = WalkScreenFolder(screenFolder, screenName);
            names.AddRange(folderNames);
            if (found is not null)
                return (found, names);
        }

        return (null, names);
    }

    private static (object? Screen, List<string> Names) WalkScreenFolder(object folder, string screenName)
    {
        var names = new List<string>();

        var screensProp = ReadPropertySafe(folder, "Screens") ??
                          ReadPropertySafe(folder, "ScreenItems");

        if (screensProp is not null)
        {
            foreach (var screenObj in EnumerateSafe(screensProp, "folder screens"))
            {
                try
                {
                    var name = ReadPropertySafe(screenObj, "Name")?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        names.Add(name!);
                        if (string.Equals(name, screenName, StringComparison.OrdinalIgnoreCase))
                            return (screenObj, names);
                    }
                }
                catch (EngineeringException ex)
                {
                    Console.Error.WriteLine($"Skipping HMI screen in folder: {ex.Message}");
                }
            }
        }

        // Recurse into sub-folders
        var folders = ReadPropertySafe(folder, "Folders") ??
                      ReadPropertySafe(folder, "Subfolders");

        if (folders is not null)
        {
            foreach (var subFolder in EnumerateSafe(folders, "screen folders"))
            {
                var (found, subNames) = WalkScreenFolder(subFolder, screenName);
                names.AddRange(subNames);
                if (found is not null)
                    return (found, names);
            }
        }

        return (null, names);
    }

    internal static string ExportScreenToXml(object screenObj, string screenName)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "tia-mcp-hmi-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var errors = new List<string>();
        var screenType = screenObj.GetType().Name;

        try
        {
            // Clean temp dir before export
            foreach (var existingFile in Directory.GetFiles(tempDir))
            {
                try { File.Delete(existingFile); }
                catch { /* ignore */ }
            }

            // Attempt 1: ExportAsDocuments (V21+ API — returns null on V16-V18 where it doesn't exist)
            var exportAsDocsMethod = screenObj.GetType().GetMethod("ExportAsDocuments",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(DirectoryInfo), typeof(string) },
                null);

            if (exportAsDocsMethod is not null)
            {
                try
                {
                    var result = exportAsDocsMethod.Invoke(screenObj, new object[] { new DirectoryInfo(tempDir), screenName });
                    var xml = ReadExportResult(result, tempDir);
                    if (xml is not null) return xml;
                    errors.Add("ExportAsDocuments produced no files");
                }
                catch (TargetInvocationException ex)
                {
                    var msg = (ex.InnerException ?? ex).Message;
                    Console.Error.WriteLine($"ExportAsDocuments failed for {screenType}: {msg}");
                    errors.Add($"ExportAsDocuments: {msg}");
                }
            }

            // Attempt 2: Export(FileInfo, ExportOptions) — standard legacy API
            var legacyMethod = screenObj.GetType().GetMethod("Export",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(FileInfo), typeof(ExportOptions) },
                null);

            if (legacyMethod is not null)
            {
                try
                {
                    var exportPath = Path.Combine(tempDir, screenName + ".xml");
                    legacyMethod.Invoke(screenObj, new object[] { new FileInfo(exportPath), ExportOptions.WithDefaults });
                    if (File.Exists(exportPath))
                        return File.ReadAllText(exportPath);
                    errors.Add("Export(FileInfo, ExportOptions) produced no file");
                }
                catch (TargetInvocationException ex)
                {
                    var msg = (ex.InnerException ?? ex).Message;
                    Console.Error.WriteLine($"Export(FileInfo, ExportOptions) failed for {screenType}: {msg}");
                    errors.Add($"Export(FileInfo, ExportOptions): {msg}");
                }
            }

            // Attempt 3: Export(FileInfo) — simplified export without options
            var simpleMethod = screenObj.GetType().GetMethod("Export",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(FileInfo) },
                null);

            if (simpleMethod is not null)
            {
                try
                {
                    var exportPath = Path.Combine(tempDir, screenName + ".xml");
                    simpleMethod.Invoke(screenObj, new object[] { new FileInfo(exportPath) });
                    if (File.Exists(exportPath))
                        return File.ReadAllText(exportPath);
                    errors.Add("Export(FileInfo) produced no file");
                }
                catch (TargetInvocationException ex)
                {
                    var msg = (ex.InnerException ?? ex).Message;
                    Console.Error.WriteLine($"Export(FileInfo) failed for {screenType}: {msg}");
                    errors.Add($"Export(FileInfo): {msg}");
                }
            }

            // Attempt 4: Scan temp dir — ExportAsDocuments may write files without returning metadata
            var files = Directory.GetFiles(tempDir);
            if (files.Length > 0)
            {
                // Find the main screen XML
                foreach (var f in files)
                {
                    try
                    {
                        var content = File.ReadAllText(f);
                        if (content.Contains("<Screen ") || content.Contains("Hmi.Screen.Screen")
                            || content.Contains("HmiScreen"))
                        {
                            return content;
                        }
                    }
                    catch { /* skip */ }
                }
                // Return largest file as last resort
                var largest = files.OrderByDescending(f => new FileInfo(f).Length).First();
                return File.ReadAllText(largest);
            }

            var tried = string.Join("; ", errors);
            throw new InvalidOperationException(
                $"Screen '{screenName}' (type: {screenType}) could not be exported. Tried: [{tried}]. " +
                "The screen type may not support XML export in this TIA version.");
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException ?? ex;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); }
                catch { /* ignore cleanup failure */ }
            }
        }
    }

    /// <summary>
    /// Read exported files from ExportAsDocuments result.
    /// Returns null if no files were produced.
    /// </summary>
    private static string? ReadExportResult(object? result, string tempDir)
    {
        // Check result state
        var stateProp = result?.GetType().GetProperty("State");
        if (stateProp is not null)
        {
            var stateValue = stateProp.GetValue(result);
            if (stateValue?.ToString() != "Success")
                return null;
        }

        // Read exported documents
        var docsProp = result?.GetType().GetProperty("ExportedDocuments");
        var exportedFiles = new List<string>();

        if (docsProp is not null)
        {
            var docs = docsProp.GetValue(result) as System.Collections.IEnumerable;
            if (docs is not null)
            {
                foreach (var doc in docs)
                {
                    var fullName = ReadPropertySafe(doc, "FullName")?.ToString();
                    if (fullName is not null && File.Exists(fullName))
                        exportedFiles.Add(fullName);
                }
            }
        }

        // Fallback: scan temp dir
        if (exportedFiles.Count == 0)
            exportedFiles.AddRange(Directory.GetFiles(tempDir));

        if (exportedFiles.Count == 0)
            return null;

        if (exportedFiles.Count == 1)
            return File.ReadAllText(exportedFiles[0]);

        // Multiple files: find the main screen file
        var mainContent = "";
        foreach (var filePath in exportedFiles)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                if (content.Contains("Hmi.Screen.Screen") || content.Contains("<Screen ")
                    || content.Contains("HmiScreen") || content.Contains("Screen "))
                {
                    mainContent = content;
                }
            }
            catch { /* skip */ }
        }

        // Fallback to largest file
        if (string.IsNullOrEmpty(mainContent))
        {
            var largest = exportedFiles.OrderByDescending(f => new FileInfo(f).Length).First();
            mainContent = File.ReadAllText(largest);
        }

        return mainContent;
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
        if (enumerable is not System.Collections.IEnumerable ie) yield break;

        System.Collections.IEnumerator enumerator;
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
                yield return current;
        }
    }
}
