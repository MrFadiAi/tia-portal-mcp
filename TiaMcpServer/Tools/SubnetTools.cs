using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    /// <summary>
    /// Project subnet lifecycle (Ethernet/PROFIBUS). All three tools are write operations gated
    /// by confirm: without confirm the worker only reads (create: duplicate check; update:
    /// located subnet + pending changes; delete: connected devices). delete_subnet additionally
    /// refuses a subnet that still has connected nodes unless force=true.
    /// </summary>
    [McpServerToolType]
    public static class SubnetTools
    {
        [McpServerTool(Name = "create_subnet")]
        [Description(
            "Create a project subnet. networkType is 'Ethernet' or 'Profibus'; highestAddress " +
            "(0-126) and transmissionSpeed (e.g. 'Baud187500') are PROFIBUS-only and rejected for " +
            "Ethernet. First call WITHOUT confirm is a DRY RUN that checks the name is free; call " +
            "again with confirm=true to create.")]
        public static async Task<string> CreateSubnet(
            OpennessWorkerClient workerClient,
            [Description("Name of the subnet to create, e.g. 'PN_1' (must be unique in the project).")] string name,
            [Description("Network type: 'Ethernet' or 'Profibus'.")] string networkType,
            [Description("PROFIBUS only: highest station address (0-126).")] int? highestAddress = null,
            [Description("PROFIBUS only: transmission speed, e.g. 'Baud187500'. Supported: Baud9600, Baud19200, Baud45450, Baud93750, Baud187500, Baud500000, Baud1500000, Baud3000000, Baud6000000, Baud12000000.")] string? transmissionSpeed = null,
            [Description("Dry run when false (default): checks the name is free. Set true to create the subnet.")] bool confirm = false,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.CreateSubnetAsync(
                name, networkType, highestAddress, transmissionSpeed, confirm, projectPath, tiaVersion).ConfigureAwait(false);
        }

        [McpServerTool(Name = "update_subnet")]
        [Description(
            "Update an existing project subnet: rename it (newName) and/or change PROFIBUS " +
            "settings (highestAddress, transmissionSpeed — rejected for Ethernet subnets). A " +
            "subnet's network type is fixed at creation. At least one change is required. First " +
            "call WITHOUT confirm is a DRY RUN that locates the subnet and lists the pending " +
            "changes; call again with confirm=true to apply.")]
        public static async Task<string> UpdateSubnet(
            OpennessWorkerClient workerClient,
            [Description("Name of the subnet to update (must exist).")] string name,
            [Description("New name for the subnet (null = keep the current name).")] string? newName = null,
            [Description("PROFIBUS only: new highest station address (0-126).")] int? highestAddress = null,
            [Description("PROFIBUS only: new transmission speed, e.g. 'Baud500000'.")] string? transmissionSpeed = null,
            [Description("Dry run when false (default): lists the pending changes. Set true to apply them.")] bool confirm = false,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.UpdateSubnetAsync(
                name, newName, highestAddress, transmissionSpeed, confirm, projectPath, tiaVersion).ConfigureAwait(false);
        }

        [McpServerTool(Name = "delete_subnet")]
        [Description(
            "Delete a project subnet. First call WITHOUT confirm is a DRY RUN that lists the " +
            "connected devices. A subnet that still has connected nodes is REFUSED unless " +
            "force=true (their network interfaces then become subnet-less). Call with " +
            "confirm=true (and force=true if nodes are connected) to delete.")]
        public static async Task<string> DeleteSubnet(
            OpennessWorkerClient workerClient,
            [Description("Name of the subnet to delete (must exist).")] string name,
            [Description("Delete even though devices are still connected (their network interfaces become subnet-less). Required for a non-empty subnet when confirm=true.")] bool force = false,
            [Description("Dry run when false (default): lists connected devices. Set true to delete the subnet.")] bool confirm = false,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.DeleteSubnetAsync(
                name, confirm, force, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
