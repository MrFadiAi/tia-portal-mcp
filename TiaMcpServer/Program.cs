using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using TiaMcpServer.Worker;

namespace TiaMcpServer
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.AddConsole(opts => opts.LogToStandardErrorThreshold = LogLevel.Trace);
            builder.Services.AddSingleton<OpennessWorkerClient>();
            builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
            await builder.Build().RunAsync();
        }
    }
}
