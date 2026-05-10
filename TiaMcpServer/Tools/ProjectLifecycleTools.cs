using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class ProjectLifecycleTools
    {
        [McpServerTool(Name = "get_project_status")]
        [Description("Get status and metadata for the active TIA Portal project.")]
        public static async Task<string> GetProjectStatus(
            OpennessWorkerClient workerClient,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null)
        {
            return await workerClient.GetProjectStatusAsync(projectPath).ConfigureAwait(false);
        }

        [McpServerTool(Name = "open_project")]
        [Description("Open a TIA Portal project and bind this MCP session to it. Requires confirm=true.")]
        public static async Task<string> OpenProject(
            OpennessWorkerClient workerClient,
            [Description("Path to the .ap21 project file to open.")] string projectPath,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false,
            [Description("Set true to allow rebinding this MCP session from a previously bound project.")] bool forceRebind = false)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with opening a project.";
            }

            return await workerClient.OpenProjectAsync(projectPath, forceRebind).ConfigureAwait(false);
        }

        [McpServerTool(Name = "create_project")]
        [Description("Create a new TIA Portal project and bind this MCP session to it. Requires confirm=true.")]
        public static async Task<string> CreateProject(
            OpennessWorkerClient workerClient,
            [Description("Directory where the project folder should be created.")] string projectDirectory,
            [Description("Name of the new TIA Portal project.")] string projectName,
            [Description("Optional project author metadata.")] string? author = null,
            [Description("Optional project comment metadata.")] string? comment = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with creating a project.";
            }

            return await workerClient.CreateProjectAsync(projectDirectory, projectName, author, comment)
                .ConfigureAwait(false);
        }

        [McpServerTool(Name = "save_project")]
        [Description("Save the active TIA Portal project. Requires confirm=true.")]
        public static async Task<string> SaveProject(
            OpennessWorkerClient workerClient,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with saving a project.";
            }

            return await workerClient.SaveProjectAsync(projectPath).ConfigureAwait(false);
        }

        [McpServerTool(Name = "save_project_as")]
        [Description("Save the active TIA Portal project to a copy directory. Requires confirm=true.")]
        public static async Task<string> SaveProjectAs(
            OpennessWorkerClient workerClient,
            [Description("Parent directory for the copied project.")] string targetDirectory,
            [Description("Name of the copied project directory.")] string targetName,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("Set true to bind this MCP session to the copied project path after save-as.")] bool rebind = true,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with saving a project copy.";
            }

            return await workerClient.SaveProjectAsAsync(projectPath, targetDirectory, targetName, rebind)
                .ConfigureAwait(false);
        }

        [McpServerTool(Name = "archive_project")]
        [Description("Archive the active TIA Portal project. Requires confirm=true.")]
        public static async Task<string> ArchiveProject(
            OpennessWorkerClient workerClient,
            [Description("Directory where the archive should be written.")] string archiveDirectory,
            [Description("Archive file name, with or without extension.")] string archiveName,
            [Description("Archive mode: None, DiscardRestorableData, Compressed, or DiscardRestorableDataAndCompressed.")] string? mode = null,
            [Description("Save the project before archiving.")] bool saveBeforeArchive = true,
            [Description("Optional path to a .ap21 project file. If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with archiving a project.";
            }

            return await workerClient.ArchiveProjectAsync(
                projectPath,
                archiveDirectory,
                archiveName,
                mode,
                saveBeforeArchive).ConfigureAwait(false);
        }

        [McpServerTool(Name = "close_project")]
        [Description("Close the active TIA Portal project and clear this MCP session binding. Requires confirm=true.")]
        public static async Task<string> CloseProject(
            OpennessWorkerClient workerClient,
            [Description("Optional path to a .ap21 project file. If omitted, closes the currently bound/open project.")] string? projectPath = null,
            [Description("Save the project before closing it.")] bool saveBeforeClose = true,
            [Description("Set to true to confirm the write operation. Required safety flag; operation is rejected when false.")] bool confirm = false)
        {
            if (!confirm)
            {
                return "Operation not confirmed. Set confirm=true to proceed with closing a project.";
            }

            return await workerClient.CloseProjectAsync(projectPath, saveBeforeClose).ConfigureAwait(false);
        }
    }
}
