using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;
using TiaMcpServer.Tools;
using TiaMcpServer.Worker;
using Xunit;

namespace TiaMcpServer.Tests;

public class TagOperationsToolTests
{
    [Theory]
    [InlineData(typeof(TagTableOperationsTool), "CreateTagTable", "create_tag_table")]
    [InlineData(typeof(TagTableOperationsTool), "DeleteTagTable", "delete_tag_table")]
    [InlineData(typeof(TagOperationsTool), "CreateTag", "create_tag")]
    [InlineData(typeof(TagOperationsTool), "UpdateTag", "update_tag")]
    [InlineData(typeof(TagOperationsTool), "DeleteTag", "delete_tag")]
    [InlineData(typeof(UserConstantOperationsTool), "CreateUserConstant", "create_user_constant")]
    [InlineData(typeof(UserConstantOperationsTool), "UpdateUserConstant", "update_user_constant")]
    [InlineData(typeof(UserConstantOperationsTool), "DeleteUserConstant", "delete_user_constant")]
    public void TagOperationToolsHaveMcpMetadata(Type toolType, string methodName, string expectedToolName)
    {
        Assert.NotNull(toolType.GetCustomAttribute<McpServerToolTypeAttribute>());

        var method = toolType.GetMethod(methodName);

        Assert.NotNull(method);
        var toolAttribute = method.GetCustomAttribute<McpServerToolAttribute>();
        Assert.NotNull(toolAttribute);
        Assert.Equal(expectedToolName, toolAttribute.Name);
        Assert.Contains("confirm=true", method.GetCustomAttribute<DescriptionAttribute>()?.Description);
    }

    [Theory]
    [InlineData("CreateTagTableAsync")]
    [InlineData("DeleteTagTableAsync")]
    [InlineData("CreateTagAsync")]
    [InlineData("UpdateTagAsync")]
    [InlineData("DeleteTagAsync")]
    [InlineData("CreateUserConstantAsync")]
    [InlineData("UpdateUserConstantAsync")]
    [InlineData("DeleteUserConstantAsync")]
    public void OpennessWorkerClientExposesTagOperationMethods(string methodName)
    {
        var method = typeof(OpennessWorkerClient).GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string>), method.ReturnType);
    }

    [Fact]
    public async Task CreateTagTableRejectsUnconfirmedRequests()
    {
        var result = await TagTableOperationsTool.CreateTagTable(
            workerClient: null!,
            tableName: "Inputs");

        Assert.Contains("Operation not confirmed", result);
        Assert.Contains("confirm=true", result);
    }

    [Fact]
    public async Task UpdateTagRejectsUnconfirmedRequests()
    {
        var result = await TagOperationsTool.UpdateTag(
            workerClient: null!,
            tableName: "Inputs",
            name: "StartButton");

        Assert.Contains("Operation not confirmed", result);
        Assert.Contains("confirm=true", result);
    }

    [Fact]
    public async Task DeleteUserConstantRejectsUnconfirmedRequests()
    {
        var result = await UserConstantOperationsTool.DeleteUserConstant(
            workerClient: null!,
            tableName: "Constants",
            name: "MaxSpeed");

        Assert.Contains("Operation not confirmed", result);
        Assert.Contains("confirm=true", result);
    }
}
