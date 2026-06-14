using System;
using System.Collections.Generic;
using Siemens.Engineering;
using Siemens.Engineering.SW.Tags;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Search tags by name (case-insensitive substring) across all tag tables of
/// one or all PLCs. Returns matches with table context + address.
/// </summary>
public static class TagSearchReader
{
    public static List<TagMatchInfo> Search(Project project, string? plcNameFilter, string pattern)
    {
        var result = new List<TagMatchInfo>();
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return result;
        }

        var pat = pattern.Trim();
        foreach (var (_, plc) in PlcSoftwareFinder.Filter(project, plcNameFilter))
        {
            SearchGroup(plc.TagTableGroup, plc.Name, "/", pat, result);
        }

        return result;
    }

    private static void SearchGroup(
        PlcTagTableGroup group, string plcName, string folderPath, string pat, List<TagMatchInfo> result)
    {
        foreach (PlcTagTable table in group.TagTables)
        {
            try
            {
                foreach (PlcTag tag in table.Tags)
                {
                    try
                    {
                        if (tag.Name.IndexOf(pat, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.Add(new TagMatchInfo
                            {
                                PlcName = plcName,
                                TableName = table.Name,
                                FolderPath = folderPath,
                                Name = tag.Name,
                                DataType = tag.DataTypeName,
                                LogicalAddress = tag.LogicalAddress,
                            });
                        }
                    }
                    catch (EngineeringException) { /* skip unreadable tag */ }
                }
            }
            catch (EngineeringException) { /* skip unreadable table */ }
        }

        foreach (PlcTagTableGroup child in group.Groups)
        {
            var childPath = folderPath == "/" ? $"/{child.Name}" : $"{folderPath}/{child.Name}";
            SearchGroup(child, plcName, childPath, pat, result);
        }
    }
}
