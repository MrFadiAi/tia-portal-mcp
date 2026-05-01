using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;
using TiaMcpServer.Tools;
using TiaMcpServer.Worker;
using Xunit;

namespace TiaMcpServer.Tests;

public class SearchEquipmentCatalogToolTests
{
    [Fact]
    public void SearchEquipmentCatalogToolHasMcpMetadata()
    {
        var type = typeof(SearchEquipmentCatalogTool);

        Assert.NotNull(type.GetCustomAttribute<McpServerToolTypeAttribute>());

        var method = type.GetMethod(
            "SearchEquipmentCatalog",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);

        var toolAttribute = method.GetCustomAttribute<McpServerToolAttribute>();
        Assert.NotNull(toolAttribute);
        Assert.Equal("search_equipment_catalog", toolAttribute.Name);

        var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description;
        Assert.Contains("installed TIA Portal V21 hardware catalog", description);
    }

    [Fact]
    public void OpennessWorkerClientExposesSearchEquipmentCatalogAsync()
    {
        var method = typeof(OpennessWorkerClient).GetMethod(
            "SearchEquipmentCatalogAsync",
            new[] { typeof(string), typeof(string) });

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string>), method.ReturnType);
    }
}
