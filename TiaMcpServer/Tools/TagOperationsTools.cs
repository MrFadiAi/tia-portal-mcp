using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class TagTableOperationsTool
    {
        [McpServerTool(Name = "create_tag_table")]
        [Description("Create a PLC tag table. Requires confirm=true.")]
        public static async Task<string> CreateTagTable(
            OpennessWorkerClient workerClient,
            [Description("Name of the tag table to create.")] string tableName,
            [Description("Optional PLC device name to target. If omitted, uses the first PLC found.")] string? plcName = null,
            [Description("Optional tag table folder path, such as '/' or '/Group/Subgroup'.")] string? folderPath = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with creating a tag table.";
            }

            return await workerClient.CreateTagTableAsync(
                plcName,
                tableName,
                folderPath,
                projectPath).ConfigureAwait(false);
        }

        [McpServerTool(Name = "delete_tag_table")]
        [Description("Delete a PLC tag table. The default tag table cannot be deleted. Requires confirm=true.")]
        public static async Task<string> DeleteTagTable(
            OpennessWorkerClient workerClient,
            [Description("Name of the tag table to delete.")] string tableName,
            [Description("Optional PLC device name to target. If omitted, uses the first PLC found.")] string? plcName = null,
            [Description("Optional tag table folder path, such as '/' or '/Group/Subgroup'.")] string? folderPath = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with deleting a tag table.";
            }

            return await workerClient.DeleteTagTableAsync(
                plcName,
                tableName,
                folderPath,
                projectPath).ConfigureAwait(false);
        }
    }

    [McpServerToolType]
    public static class TagOperationsTool
    {
        [McpServerTool(Name = "create_tag")]
        [Description("Create a PLC tag in an existing tag table. Requires confirm=true.")]
        public static async Task<string> CreateTag(
            OpennessWorkerClient workerClient,
            [Description("Name of the tag table containing the tag.")] string tableName,
            [Description("Name of the tag to create.")] string name,
            [Description("TIA Portal data type, such as Bool, Int, Real, or a PLC type name.")] string dataType,
            [Description("Optional PLC device name to target. If omitted, uses the first PLC found.")] string? plcName = null,
            [Description("Optional tag table folder path, such as '/' or '/Group/Subgroup'.")] string? folderPath = null,
            [Description("Optional logical address, such as %I0.0. Use empty to leave unassigned.")] string? logicalAddress = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with creating a tag.";
            }

            return await workerClient.CreateTagAsync(
                plcName,
                tableName,
                folderPath,
                name,
                dataType,
                logicalAddress,
                projectPath).ConfigureAwait(false);
        }

        [McpServerTool(Name = "update_tag")]
        [Description("Update a PLC tag name, data type, logical address, external access flags, or safety flag. Requires confirm=true.")]
        public static async Task<string> UpdateTag(
            OpennessWorkerClient workerClient,
            [Description("Name of the tag table containing the tag.")] string tableName,
            [Description("Current name of the tag to update.")] string name,
            [Description("Optional PLC device name to target. If omitted, uses the first PLC found.")] string? plcName = null,
            [Description("Optional tag table folder path, such as '/' or '/Group/Subgroup'.")] string? folderPath = null,
            [Description("Optional new tag name.")] string? newName = null,
            [Description("Optional new TIA Portal data type.")] string? dataType = null,
            [Description("Optional new logical address.")] string? logicalAddress = null,
            [Description("Optional ExternalAccessible flag.")] bool? externalAccessible = null,
            [Description("Optional ExternalVisible flag.")] bool? externalVisible = null,
            [Description("Optional ExternalWritable flag.")] bool? externalWritable = null,
            [Description("Optional IsSafety flag.")] bool? isSafety = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with updating a tag.";
            }

            return await workerClient.UpdateTagAsync(
                plcName,
                tableName,
                folderPath,
                name,
                newName,
                dataType,
                logicalAddress,
                externalAccessible,
                externalVisible,
                externalWritable,
                isSafety,
                projectPath).ConfigureAwait(false);
        }

        [McpServerTool(Name = "delete_tag")]
        [Description("Delete a PLC tag from an existing tag table. Requires confirm=true.")]
        public static async Task<string> DeleteTag(
            OpennessWorkerClient workerClient,
            [Description("Name of the tag table containing the tag.")] string tableName,
            [Description("Name of the tag to delete.")] string name,
            [Description("Optional PLC device name to target. If omitted, uses the first PLC found.")] string? plcName = null,
            [Description("Optional tag table folder path, such as '/' or '/Group/Subgroup'.")] string? folderPath = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with deleting a tag.";
            }

            return await workerClient.DeleteTagAsync(
                plcName,
                tableName,
                folderPath,
                name,
                projectPath).ConfigureAwait(false);
        }
    }

    [McpServerToolType]
    public static class UserConstantOperationsTool
    {
        [McpServerTool(Name = "create_user_constant")]
        [Description("Create a PLC user constant in an existing tag table. Requires confirm=true.")]
        public static async Task<string> CreateUserConstant(
            OpennessWorkerClient workerClient,
            [Description("Name of the tag table containing the user constant.")] string tableName,
            [Description("Name of the user constant to create.")] string name,
            [Description("TIA Portal data type for the user constant.")] string dataType,
            [Description("Value for the user constant.")] string value,
            [Description("Optional PLC device name to target. If omitted, uses the first PLC found.")] string? plcName = null,
            [Description("Optional tag table folder path, such as '/' or '/Group/Subgroup'.")] string? folderPath = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with creating a user constant.";
            }

            return await workerClient.CreateUserConstantAsync(
                plcName,
                tableName,
                folderPath,
                name,
                dataType,
                value,
                projectPath).ConfigureAwait(false);
        }

        [McpServerTool(Name = "update_user_constant")]
        [Description("Update a PLC user constant data type or value. Requires confirm=true.")]
        public static async Task<string> UpdateUserConstant(
            OpennessWorkerClient workerClient,
            [Description("Name of the tag table containing the user constant.")] string tableName,
            [Description("Name of the user constant to update.")] string name,
            [Description("Optional PLC device name to target. If omitted, uses the first PLC found.")] string? plcName = null,
            [Description("Optional tag table folder path, such as '/' or '/Group/Subgroup'.")] string? folderPath = null,
            [Description("Optional new TIA Portal data type.")] string? dataType = null,
            [Description("Optional new value.")] string? value = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with updating a user constant.";
            }

            return await workerClient.UpdateUserConstantAsync(
                plcName,
                tableName,
                folderPath,
                name,
                dataType,
                value,
                projectPath).ConfigureAwait(false);
        }

        [McpServerTool(Name = "delete_user_constant")]
        [Description("Delete a PLC user constant from an existing tag table. Requires confirm=true.")]
        public static async Task<string> DeleteUserConstant(
            OpennessWorkerClient workerClient,
            [Description("Name of the tag table containing the user constant.")] string tableName,
            [Description("Name of the user constant to delete.")] string name,
            [Description("Optional PLC device name to target. If omitted, uses the first PLC found.")] string? plcName = null,
            [Description("Optional tag table folder path, such as '/' or '/Group/Subgroup'.")] string? folderPath = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with deleting a user constant.";
            }

            return await workerClient.DeleteUserConstantAsync(
                plcName,
                tableName,
                folderPath,
                name,
                projectPath).ConfigureAwait(false);
        }
    }
}
