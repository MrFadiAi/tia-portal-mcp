using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;
using TiaMcpServer.Tools;
using TiaMcpServer.Worker;
using Xunit;

namespace TiaMcpServer.Tests;

public class CompileCheckToolTests
{
    [Fact]
    public void CompileCheckToolHasMcpMetadata()
    {
        var type = typeof(CompileCheckTool);

        Assert.NotNull(type.GetCustomAttribute<McpServerToolTypeAttribute>());

        var method = type.GetMethod(
            "CompileCheck",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);

        var toolAttribute = method.GetCustomAttribute<McpServerToolAttribute>();
        Assert.NotNull(toolAttribute);
        Assert.Equal("compile_check", toolAttribute.Name);

        var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description;
        Assert.Contains("compile", description);
        Assert.Contains("errors", description);
        Assert.Contains("warnings", description);
    }

    [Fact]
    public void OpennessWorkerClientExposesCompileCheckAsync()
    {
        var method = typeof(OpennessWorkerClient).GetMethod(
            "CompileCheckAsync",
            new[] { typeof(string), typeof(string), typeof(string) });

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string>), method.ReturnType);
    }
}
