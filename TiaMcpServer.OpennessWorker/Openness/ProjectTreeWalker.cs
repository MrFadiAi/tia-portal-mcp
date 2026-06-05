using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;
using Siemens.Engineering.SW.Units;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

public class ProjectTreeWalker
{
    public List<ProjectTreeNode> Walk(Project project)
    {
        var rootNodes = new List<ProjectTreeNode>();

        foreach (Device device in project.Devices)
        {
            var children = new List<ProjectTreeNode>();

            foreach (var plcSoftware in FindPlcSoftwareInDevice(device))
            {
                children.Add(WalkPlcSoftware(device.Name, plcSoftware));
            }

            rootNodes.Add(new ProjectTreeNode
            {
                Name = device.Name,
                NodeType = "Device",
                Details = new Dictionary<string, string>
                {
                    ["Path"] = device.Name
                },
                Children = children
            });
        }

        return rootNodes;
    }

    private static IEnumerable<PlcSoftware> FindPlcSoftwareInDevice(Device device)
    {
        return FindPlcSoftwareInDeviceItems(device.DeviceItems);
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

    private static ProjectTreeNode WalkPlcSoftware(string deviceName, PlcSoftware plcSoftware)
    {
        var children = new List<ProjectTreeNode>
        {
            WalkBlockGroup(plcSoftware.BlockGroup, CombinePath(deviceName, "Blocks"), softwareUnitName: null),
            WalkTagTableGroup(plcSoftware.TagTableGroup, CombinePath(deviceName, "TagTables")),
            WalkTypeGroup(plcSoftware.TypeGroup, CombinePath(deviceName, "Types"))
        };

        // Walk system block groups (SFBs, SFCs, etc.) if available
        var systemBlocks = WalkSystemBlockGroups(plcSoftware.BlockGroup, CombinePath(deviceName, "SystemBlocks"));
        if (systemBlocks is not null)
        {
            children.Add(systemBlocks);
        }

        children.AddRange(WalkSoftwareUnits(deviceName, plcSoftware));

        return new ProjectTreeNode
        {
            Name = plcSoftware.Name,
            NodeType = "PlcSoftware",
            Details = new Dictionary<string, string>
            {
                ["Path"] = deviceName
            },
            Children = children
        };
    }

    private static IEnumerable<ProjectTreeNode> WalkSoftwareUnits(string deviceName, PlcSoftware plcSoftware)
    {
        PlcUnitProvider? unitProvider = null;

        try
        {
            unitProvider = plcSoftware.GetService<PlcUnitProvider>();
        }
        catch (EngineeringException ex)
        {
            Console.Error.WriteLine($"Skipping software units for PLC software '{plcSoftware.Name}': {ex.Message}");
        }

        if (unitProvider is null)
        {
            yield break;
        }

        foreach (PlcUnit unit in unitProvider.UnitGroup.Units)
        {
            var unitPath = CombinePath(deviceName, "Units", unit.Name);

            yield return new ProjectTreeNode
            {
                Name = unit.Name,
                NodeType = "SoftwareUnit",
                Details = new Dictionary<string, string>
                {
                    ["Path"] = unitPath
                },
                Children = new List<ProjectTreeNode>
                {
                    WalkBlockGroup(unit.BlockGroup, CombinePath(unitPath, "Blocks"), unit.Name),
                    WalkTagTableGroup(unit.TagTableGroup, CombinePath(unitPath, "TagTables")),
                    WalkTypeGroup(unit.TypeGroup, CombinePath(unitPath, "Types"))
                }
            };
        }
    }

    private static ProjectTreeNode WalkBlockGroup(PlcBlockGroup group, string path, string? softwareUnitName)
    {
        var children = new List<ProjectTreeNode>();

        foreach (PlcBlock block in group.Blocks)
        {
            try
            {
                var details = new Dictionary<string, string>
                {
                    ["Path"] = CombinePath(path, block.Name),
                    ["Number"] = block.Number.ToString(),
                    ["ProgrammingLanguage"] = block.ProgrammingLanguage.ToString()
                };

                if (softwareUnitName is not null)
                {
                    details["SoftwareUnit"] = softwareUnitName;
                }

                children.Add(new ProjectTreeNode
                {
                    Name = block.Name,
                    NodeType = block switch
                    {
                        OB => "OB",
                        FB => "FB",
                        FC => "FC",
                        GlobalDB => "GlobalDB",
                        InstanceDB => "InstanceDB",
                        ArrayDB => "ArrayDB",
                        _ => "Block"
                    },
                    Details = details
                });
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping a block while walking block group '{group.Name}': {ex.Message}");
            }
        }

        foreach (PlcBlockGroup childGroup in group.Groups)
        {
            try
            {
                children.Add(WalkBlockGroup(childGroup, CombinePath(path, childGroup.Name), softwareUnitName));
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping a nested block group while walking block group '{group.Name}': {ex.Message}");
            }
        }

        return new ProjectTreeNode
        {
            Name = group.Name,
            NodeType = "BlockFolder",
            Details = new Dictionary<string, string>
            {
                ["Path"] = path
            },
            Children = children
        };
    }

    private static ProjectTreeNode WalkTagTableGroup(PlcTagTableGroup group, string path)
    {
        var children = new List<ProjectTreeNode>();

        foreach (PlcTagTable table in group.TagTables)
        {
            try
            {
                children.Add(new ProjectTreeNode
                {
                    Name = table.Name,
                    NodeType = "TagTable",
                    Details = new Dictionary<string, string>
                    {
                        ["Path"] = CombinePath(path, table.Name)
                    }
                });
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping a tag table while walking tag table group '{group.Name}': {ex.Message}");
            }
        }

        foreach (PlcTagTableGroup childGroup in group.Groups)
        {
            try
            {
                children.Add(WalkTagTableGroup(childGroup, CombinePath(path, childGroup.Name)));
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping a nested tag table group while walking tag table group '{group.Name}': {ex.Message}");
            }
        }

        return new ProjectTreeNode
        {
            Name = group.Name,
            NodeType = "TagTableFolder",
            Details = new Dictionary<string, string>
            {
                ["Path"] = path
            },
            Children = children
        };
    }

    private static ProjectTreeNode WalkTypeGroup(PlcTypeGroup group, string path)
    {
        var children = new List<ProjectTreeNode>();

        foreach (PlcType type in group.Types)
        {
            try
            {
                children.Add(new ProjectTreeNode
                {
                    Name = type.Name,
                    NodeType = "Type",
                    Details = new Dictionary<string, string>
                    {
                        ["Path"] = CombinePath(path, type.Name)
                    }
                });
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping a type while walking type group '{group.Name}': {ex.Message}");
            }
        }

        foreach (PlcTypeGroup childGroup in group.Groups)
        {
            try
            {
                children.Add(WalkTypeGroup(childGroup, CombinePath(path, childGroup.Name)));
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping a nested type group while walking type group '{group.Name}': {ex.Message}");
            }
        }

        return new ProjectTreeNode
        {
            Name = group.Name,
            NodeType = "TypeFolder",
            Details = new Dictionary<string, string>
            {
                ["Path"] = path
            },
            Children = children
        };
    }

    /// <summary>
    /// Walks system block groups (SFBs, SFCs, Program resources, etc.) using reflection
    /// since the SystemBlockGroups property may not be available in all API versions.
    /// </summary>
    private static ProjectTreeNode? WalkSystemBlockGroups(object blockGroup, string path)
    {
        // Try to find SystemBlockGroups property
        var sbgProperty = blockGroup.GetType().GetProperty("SystemBlockGroups",
            BindingFlags.Instance | BindingFlags.Public);

        if (sbgProperty is null)
        {
            return null;
        }

        object? sbgValue;
        try
        {
            sbgValue = sbgProperty.GetValue(blockGroup);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is EngineeringException)
        {
            Console.Error.WriteLine($"Skipping system block groups: {ex.InnerException!.Message}");
            return null;
        }
        catch (EngineeringException ex)
        {
            Console.Error.WriteLine($"Skipping system block groups: {ex.Message}");
            return null;
        }

        if (sbgValue is not IEnumerable systemGroups)
        {
            return null;
        }

        var children = new List<ProjectTreeNode>();

        foreach (var systemGroup in EnumerateSafe(systemGroups, "system block groups"))
        {
            var groupName = ReadPropertySafe(systemGroup, "Name", "system block group")?.ToString();
            if (string.IsNullOrEmpty(groupName))
            {
                continue;
            }

            var groupPath = CombinePath(path, groupName!);
            var groupNode = WalkSystemBlockGroup(systemGroup, groupPath);
            if (groupNode is not null)
            {
                children.Add(groupNode);
            }
        }

        if (children.Count == 0)
        {
            return null;
        }

        return new ProjectTreeNode
        {
            Name = "System Blocks",
            NodeType = "SystemBlockFolder",
            Details = new Dictionary<string, string>
            {
                ["Path"] = path
            },
            Children = children
        };
    }

    private static ProjectTreeNode? WalkSystemBlockGroup(object systemGroup, string path)
    {
        var children = new List<ProjectTreeNode>();

        // Walk blocks in this system group
        var blocks = EnumerateSafe(
            ReadPropertySafe(systemGroup, "Blocks", "system group blocks"),
            "system blocks");

        foreach (var block in blocks)
        {
            var blockName = ReadPropertySafe(block, "Name", "system block")?.ToString();
            if (string.IsNullOrEmpty(blockName))
            {
                continue;
            }

            var details = new Dictionary<string, string>
            {
                ["Path"] = CombinePath(path, blockName!)
            };

            var number = ReadPropertySafe(block, "Number", "system block number");
            if (number is not null)
            {
                details["Number"] = number.ToString() ?? "";
            }

            var lang = ReadPropertySafe(block, "ProgrammingLanguage", "system block language");
            if (lang is not null)
            {
                details["ProgrammingLanguage"] = lang.ToString() ?? "";
            }

            children.Add(new ProjectTreeNode
            {
                Name = blockName!,
                NodeType = "SystemBlock",
                Details = details
            });
        }

        // Walk sub-groups
        var subGroups = EnumerateSafe(
            ReadPropertySafe(systemGroup, "Groups", "system group sub-groups"),
            "system sub-groups");

        foreach (var subGroup in subGroups)
        {
            var subGroupName = ReadPropertySafe(subGroup, "Name", "system sub-group")?.ToString();
            if (string.IsNullOrEmpty(subGroupName))
            {
                continue;
            }

            var subGroupPath = CombinePath(path, subGroupName!);
            var subGroupNode = WalkSystemBlockGroup(subGroup, subGroupPath);
            if (subGroupNode is not null)
            {
                children.Add(subGroupNode);
            }
        }

        if (children.Count == 0)
        {
            return null;
        }

        var groupName = ReadPropertySafe(systemGroup, "Name", "system block group")?.ToString() ?? "System Blocks";
        return new ProjectTreeNode
        {
            Name = groupName,
            NodeType = "SystemBlockFolder",
            Details = new Dictionary<string, string>
            {
                ["Path"] = path
            },
            Children = children
        };
    }

    private static object? ReadPropertySafe(object instance, string propertyName, string description)
    {
        try
        {
            return instance.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(instance);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is EngineeringException engineeringException)
        {
            Console.Error.WriteLine($"Skipping {description}: {engineeringException.Message}");
            return null;
        }
        catch (EngineeringException ex)
        {
            Console.Error.WriteLine($"Skipping {description}: {ex.Message}");
            return null;
        }
    }

    private static IEnumerable<object> EnumerateSafe(object? enumerable, string description)
    {
        if (enumerable is null)
        {
            yield break;
        }

        if (enumerable is not IEnumerable ie)
        {
            yield break;
        }

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
                if (!enumerator.MoveNext())
                {
                    yield break;
                }
                current = enumerator.Current;
            }
            catch (EngineeringException ex)
            {
                Console.Error.WriteLine($"Skipping an entry in {description}: {ex.Message}");
                yield break;
            }

            if (current is not null)
            {
                yield return current;
            }
        }
    }

    private static string CombinePath(params string[] segments)
    {
        return string.Join("/", segments);
    }
}
