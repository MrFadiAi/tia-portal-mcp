using System;
using System.Collections.Generic;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;
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
                children.Add(WalkPlcSoftware(plcSoftware));
            }

            rootNodes.Add(new ProjectTreeNode
            {
                Name = device.Name,
                NodeType = "Device",
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

    private static ProjectTreeNode WalkPlcSoftware(PlcSoftware plcSoftware)
    {
        return new ProjectTreeNode
        {
            Name = plcSoftware.Name,
            NodeType = "PlcSoftware",
            Children = new List<ProjectTreeNode>
            {
                WalkBlockGroup(plcSoftware.BlockGroup),
                WalkTagTableGroup(plcSoftware.TagTableGroup),
                WalkTypeGroup(plcSoftware.TypeGroup)
            }
        };
    }

    private static ProjectTreeNode WalkBlockGroup(PlcBlockGroup group)
    {
        var children = new List<ProjectTreeNode>();

        foreach (PlcBlock block in group.Blocks)
        {
            try
            {
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
                    Details = new Dictionary<string, string>
                    {
                        ["Number"] = block.Number.ToString(),
                        ["ProgrammingLanguage"] = block.ProgrammingLanguage.ToString()
                    }
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
                children.Add(WalkBlockGroup(childGroup));
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
            Children = children
        };
    }

    private static ProjectTreeNode WalkTagTableGroup(PlcTagTableGroup group)
    {
        var children = new List<ProjectTreeNode>();

        foreach (PlcTagTable table in group.TagTables)
        {
            try
            {
                children.Add(new ProjectTreeNode
                {
                    Name = table.Name,
                    NodeType = "TagTable"
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
                children.Add(WalkTagTableGroup(childGroup));
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
            Children = children
        };
    }

    private static ProjectTreeNode WalkTypeGroup(PlcTypeGroup group)
    {
        var children = new List<ProjectTreeNode>();

        foreach (PlcType type in group.Types)
        {
            try
            {
                children.Add(new ProjectTreeNode
                {
                    Name = type.Name,
                    NodeType = "Type"
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
                children.Add(WalkTypeGroup(childGroup));
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
            Children = children
        };
    }
}
