using System.ComponentModel;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer.Tools
{
    [McpServerToolType]
    public static class SearchEquipmentCatalogTool
    {
        [McpServerTool(Name = "search_equipment_catalog")]
        [Description("Search the installed TIA Portal hardware catalog for devices, modules, and components. Returns matching catalog entries with type identifiers that can be used with add_network_device. Includes locally installed GSD/HSP packages.")]
        public static async Task<string> SearchEquipmentCatalog(
            OpennessWorkerClient workerClient,
            [Description("Required search text matched against type name, article number, or description.")] string query,
            [Description("Optional path to a TIA Portal project file (.ap16, .ap18, .ap19, .ap21). If omitted, uses the project currently open in TIA Portal.")] string? projectPath = null,
            [Description("TIA Portal major version (16, 18, 21). Omit for auto-detect.")] int? tiaVersion = null)
        {
            return await workerClient.SearchEquipmentCatalogAsync(query, projectPath, tiaVersion).ConfigureAwait(false);
        }
    }
}
