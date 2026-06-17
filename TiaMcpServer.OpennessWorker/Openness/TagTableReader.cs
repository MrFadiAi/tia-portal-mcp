using Siemens.Engineering;
using Siemens.Engineering.SW.Tags;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class TagTableReader
{
    public static List<TagTableInfo> ReadAll(Project project, string? plcName)
    {
        var plcSoftware = PlcSoftwareLocator.Find(project, plcName);

        var result = new List<TagTableInfo>();

        CollectTablesFromGroup(plcSoftware.TagTableGroup, folderPath: "/", result);

        return result;
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
                    LogicalAddress = tag.LogicalAddress,
                    Comment = TryReadComment(tag.Comment)
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

    /// <summary>
    /// Best-effort extraction of a display string from a tag's multilingual comment
    /// (<c>PlcTag.Comment</c> is a <see cref="MultilingualText"/>, not a string). Comment is optional
    /// metadata; it must never fail a tag read, so any error is swallowed and null is returned.
    /// </summary>
    private static string? TryReadComment(MultilingualText? ml)
    {
        if (ml is null)
        {
            return null;
        }

        try
        {
            foreach (MultilingualTextItem item in ml.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.Text))
                {
                    return item.Text;
                }
            }
        }
        catch
        {
            // Swallow: comment is best-effort metadata.
        }

        return null;
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
