using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Reads HMI screen data using reflection for version-independent access.
/// V21+: Siemens.Engineering.WinCCUnified.dll — HmiTarget → HmiSoftware → Screens
/// V16-V18: Siemens.Engineering.Hmi — HmiSoftware → HmiScreen
/// </summary>
public static class HmiScreenReader
{
    public static List<HmiDeviceInfo> Read(Project project, string? deviceName, string? mode, string? screenName)
    {
        var result = new List<HmiDeviceInfo>();
        var listOnly = string.Equals(mode, "list", StringComparison.OrdinalIgnoreCase);

        foreach (Device device in project.Devices)
        {
            if (!string.IsNullOrEmpty(deviceName) &&
                !string.Equals(device.Name, deviceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var hmiData = ReadDeviceHmi(device, listOnly, screenName);
                if (hmiData is not null)
                {
                    result.Add(hmiData);
                }
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping device '{device.Name}' while reading HMI: {ex.Message}");
            }
        }

        return result;
    }

    private static HmiDeviceInfo? ReadDeviceHmi(Device device, bool listOnly, string? screenName)
    {
        foreach (DeviceItem item in device.DeviceItems)
        {
            try
            {
                var container = item.GetService<SoftwareContainer>();
                if (container?.Software is null)
                {
                    continue;
                }

                var software = container.Software;
                var softwareType = software.GetType();

                // Check if this is HMI software (varies by version)
                if (!IsHmiSoftware(softwareType))
                {
                    continue;
                }

                var typeIdentifier = ReadPropertySafe(device, "TypeIdentifier")?.ToString() ?? "";
                var screens = ReadScreensFromSoftware(software, listOnly, screenName);

                return new HmiDeviceInfo
                {
                    DeviceName = device.Name,
                    TypeIdentifier = typeIdentifier,
                    Screens = screens
                };
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping device item while reading HMI from device '{device.Name}': {ex.Message}");
            }
        }

        return null;
    }

    private static bool IsHmiSoftware(Type type)
    {
        // Check for known HMI software types across versions
        var name = type.Name;
        return name.Contains("HmiSoftware") ||
               name.Contains("HmiTarget") ||
               name.Contains("UnifiedHmiSoftware") ||
               name.Contains("ScreenProvider");
    }

    private static List<HmiScreenInfo> ReadScreensFromSoftware(object software, bool listOnly, string? screenName)
    {
        var screens = new List<HmiScreenInfo>();

        // Try to get the Screens collection via reflection
        var screensCollection = ReadPropertySafe(software, "Screens");
        if (screensCollection is null)
        {
            // V21+ may use HmiSoftware.ScreenFolder or similar
            var screenFolder = ReadPropertySafe(software, "ScreenFolder") ??
                               ReadPropertySafe(software, "Folders");

            if (screenFolder is not null)
            {
                WalkScreenFolder(screenFolder, screens, listOnly, screenName);
            }

            return screens;
        }

        foreach (var screenObj in EnumerateSafe(screensCollection, "HMI screens"))
        {
            try
            {
                var screenInfo = ReadScreen(screenObj, listOnly, screenName);
                if (screenInfo is not null)
                {
                    screens.Add(screenInfo);
                }
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping HMI screen: {ex.Message}");
            }
        }

        return screens;
    }

    private static void WalkScreenFolder(object folder, List<HmiScreenInfo> screens, bool listOnly, string? screenName)
    {
        // Walk screens in this folder
        var screensProp = ReadPropertySafe(folder, "Screens") ??
                          ReadPropertySafe(folder, "ScreenItems");

        if (screensProp is not null)
        {
            foreach (var screenObj in EnumerateSafe(screensProp, "folder screens"))
            {
                try
                {
                    var screenInfo = ReadScreen(screenObj, listOnly, screenName);
                    if (screenInfo is not null)
                    {
                        screens.Add(screenInfo);
                    }
                }
                catch (EngineeringException ex)
                {
                    Console.Error.WriteLine($"Skipping HMI screen in folder: {ex.Message}");
                }
            }
        }

        // Walk sub-folders
        var folders = ReadPropertySafe(folder, "Folders") ??
                      ReadPropertySafe(folder, "Subfolders");

        if (folders is not null)
        {
            foreach (var subFolder in EnumerateSafe(folders, "screen folders"))
            {
                WalkScreenFolder(subFolder, screens, listOnly, screenName);
            }
        }
    }

    private static HmiScreenInfo? ReadScreen(object screenObj, bool listOnly, string? screenName)
    {
        var name = ReadPropertySafe(screenObj, "Name")?.ToString();
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        // In detail mode with a screenName filter, skip non-matching screens
        if (!string.IsNullOrEmpty(screenName) &&
            !string.Equals(name, screenName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var screenInfo = new HmiScreenInfo
        {
            ScreenName = name!
        };

        // In list mode, just count items — don't read full details
        if (listOnly)
        {
            var items = ReadPropertySafe(screenObj, "ScreenItems") ??
                        ReadPropertySafe(screenObj, "Items") ??
                        ReadPropertySafe(screenObj, "Children");

            if (items is not null)
            {
                var count = 0;
                foreach (var _ in EnumerateSafe(items, $"screen '{name}' items"))
                {
                    count++;
                }
                screenInfo.ItemCount = count;
            }

            return screenInfo;
        }

        // Full detail mode — walk screen items (buttons, IO fields, etc.)
        var detailItems = ReadPropertySafe(screenObj, "ScreenItems") ??
                          ReadPropertySafe(screenObj, "Items") ??
                          ReadPropertySafe(screenObj, "Children");

        if (detailItems is not null)
        {
            foreach (var itemObj in EnumerateSafe(detailItems, $"screen '{name}' items"))
            {
                try
                {
                    var itemInfo = ReadScreenItem(itemObj);
                    if (itemInfo is not null)
                    {
                        screenInfo.Items.Add(itemInfo);
                    }
                }
                catch (EngineeringException ex)
                {
                    Console.Error.WriteLine($"Skipping screen item in '{name}': {ex.Message}");
                }
            }
        }

        return screenInfo;
    }

    private static HmiScreenItemInfo? ReadScreenItem(object itemObj)
    {
        var itemName = ReadPropertySafe(itemObj, "Name")?.ToString() ?? "";
        var typeName = ReadPropertySafe(itemObj, "TypeName")?.ToString() ??
                       ReadPropertySafe(itemObj, "Type")?.ToString() ??
                       itemObj.GetType().Name;

        var itemInfo = new HmiScreenItemInfo
        {
            Name = itemName,
            TypeName = typeName ?? ""
        };

        // Read position/size if available
        var x = ReadPropertySafe(itemObj, "Left") ?? ReadPropertySafe(itemObj, "X");
        var y = ReadPropertySafe(itemObj, "Top") ?? ReadPropertySafe(itemObj, "Y");
        var w = ReadPropertySafe(itemObj, "Width");
        var h = ReadPropertySafe(itemObj, "Height");

        if (x is not null) itemInfo.X = Convert.ToDouble(x);
        if (y is not null) itemInfo.Y = Convert.ToDouble(y);
        if (w is not null) itemInfo.Width = Convert.ToDouble(w);
        if (h is not null) itemInfo.Height = Convert.ToDouble(h);

        // Read tag bindings (Dynamizations)
        var dynamizations = ReadPropertySafe(itemObj, "Dynamizations") ??
                            ReadPropertySafe(itemObj, "TagBindings");

        if (dynamizations is not null)
        {
            foreach (var dyn in EnumerateSafe(dynamizations, $"item '{itemName}' dynamizations"))
            {
                try
                {
                    var binding = ReadTagBinding(dyn);
                    if (binding is not null)
                    {
                        itemInfo.TagBindings.Add(binding);
                    }
                }
                catch (EngineeringException ex)
                {
                    Console.Error.WriteLine($"Skipping dynamization: {ex.Message}");
                }
            }
        }

        // Read events
        var events = ReadPropertySafe(itemObj, "Events");
        if (events is not null)
        {
            foreach (var evt in EnumerateSafe(events, $"item '{itemName}' events"))
            {
                try
                {
                    var eventInfo = ReadEvent(evt);
                    if (eventInfo is not null)
                    {
                        itemInfo.Events.Add(eventInfo);
                    }
                }
                catch (EngineeringException ex)
                {
                    Console.Error.WriteLine($"Skipping event: {ex.Message}");
                }
            }
        }

        return itemInfo;
    }

    private static HmiTagBindingInfo? ReadTagBinding(object dynamization)
    {
        var propName = ReadPropertySafe(dynamization, "PropertyName")?.ToString() ??
                       ReadPropertySafe(dynamization, "Property")?.ToString() ?? "";

        // Try to get the tag name from various possible properties
        var tagName = ReadPropertySafe(dynamization, "TagName")?.ToString() ??
                      ReadPropertySafe(dynamization, "Tag")?.ToString() ??
                      ReadPropertySafe(dynamization, "VariableName")?.ToString() ?? "";

        // Some versions store tag reference as an object with a Name property
        if (string.IsNullOrEmpty(tagName))
        {
            var tagRef = ReadPropertySafe(dynamization, "TagReference") ??
                         ReadPropertySafe(dynamization, "LinkedTag");
            if (tagRef is not null)
            {
                tagName = ReadPropertySafe(tagRef, "Name")?.ToString() ??
                          ReadPropertySafe(tagRef, "FullName")?.ToString() ?? "";
            }
        }

        if (string.IsNullOrEmpty(propName) && string.IsNullOrEmpty(tagName))
        {
            return null;
        }

        return new HmiTagBindingInfo
        {
            PropertyName = propName,
            TagName = tagName
        };
    }

    private static HmiEventInfo? ReadEvent(object evt)
    {
        var eventName = ReadPropertySafe(evt, "Name")?.ToString() ??
                        ReadPropertySafe(evt, "EventName")?.ToString() ?? "";
        if (string.IsNullOrEmpty(eventName))
        {
            return null;
        }

        return new HmiEventInfo
        {
            EventName = eventName,
            FunctionName = ReadPropertySafe(evt, "ActionType")?.ToString() ??
                           ReadPropertySafe(evt, "Type")?.ToString()
        };
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
