using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Tags;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class TagTableReader
{
    public static List<TagTableInfo> ReadAll(Project project, string? plcName)
    {
        var plcSoftware = FindPlcSoftware(project, plcName);

        var result = new List<TagTableInfo>();

        CollectTablesFromGroup(plcSoftware.TagTableGroup, folderPath: "/", result);

        return result;
    }

    private static PlcSoftware FindPlcSoftware(Project project, string? plcName)
    {
        foreach (Device device in project.Devices)
        {
            if (plcName is not null && !string.Equals(device.Name, plcName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var plcSoftware in FindPlcSoftwareInDeviceItems(device.DeviceItems))
            {
                return plcSoftware;
            }
        }

        var detail = plcName is not null
            ? $" named '{plcName}'"
            : string.Empty;

        throw new InvalidOperationException($"No PLC software{detail} was found in the project.");
    }

    private static IEnumerable<PlcSoftware> FindPlcSoftwareInDeviceItems(DeviceItemComposition items)
    {
        foreach (DeviceItem item in items)
        {
            PlcSoftware? plcSoftware = null;

            try
            {
                var container = item.GetService<SoftwareContainer>();
                plcSoftware = container?.Software as PlcSoftware;
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping a device item while locating PLC software: {ex.Message}");
            }

            if (plcSoftware is not null)
            {
                yield return plcSoftware;
            }

            foreach (var child in FindPlcSoftwareInDeviceItems(item.DeviceItems))
            {
                yield return child;
            }
        }
    }

    private static void CollectTablesFromGroup(
        PlcTagTableGroup group,
        string folderPath,
        List<TagTableInfo> result)
    {
        foreach (PlcTagTable table in group.TagTables)
        {
            try
            {
                result.Add(ReadTagTable(table, folderPath));
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine(
                    $"Skipping a tag table while reading tag table group '{group.Name}': {ex.Message}");
            }
        }

        foreach (PlcTagTableGroup childGroup in group.Groups)
        {
            try
            {
                var childPath = folderPath == "/"
                    ? $"/{childGroup.Name}"
                    : $"{folderPath}/{childGroup.Name}";

                CollectTablesFromGroup(childGroup, childPath, result);
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine(
                    $"Skipping a nested tag table group while reading tag table group '{group.Name}': {ex.Message}");
            }
        }
    }

    private static TagTableInfo ReadTagTable(PlcTagTable table, string folderPath)
    {
        var tagTableInfo = new TagTableInfo
        {
            Name = table.Name,
            FolderPath = folderPath,
            IsDefault = table.IsDefault,
            Tags = ReadTags(table),
            UserConstants = ReadUserConstants(table)
        };

        return tagTableInfo;
    }

    private static List<TagInfo> ReadTags(PlcTagTable table)
    {
        var tags = new List<TagInfo>();

        foreach (PlcTag tag in table.Tags)
        {
            try
            {
                tags.Add(new TagInfo
                {
                    Name = tag.Name,
                    DataType = tag.DataTypeName,
                    LogicalAddress = tag.LogicalAddress
                });
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine(
                    $"Skipping a tag while reading tag table '{table.Name}': {ex.Message}");
            }
        }

        return tags;
    }

    private static List<UserConstantInfo> ReadUserConstants(PlcTagTable table)
    {
        var constants = new List<UserConstantInfo>();

        foreach (PlcUserConstant c in table.UserConstants)
        {
            try
            {
                constants.Add(new UserConstantInfo
                {
                    Name = c.Name,
                    DataType = c.DataTypeName,
                    Value = c.Value?.ToString() ?? string.Empty
                });
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine(
                    $"Skipping a user constant while reading tag table '{table.Name}': {ex.Message}");
            }
        }

        return constants;
    }
}
