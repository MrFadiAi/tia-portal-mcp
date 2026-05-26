using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using TiaMcpServer.Contracts;
using TiaMcpServer.Safety;
using TiaMcpServer.Worker;

namespace TiaMcpServer
{
    internal static class Program
    {
        static Program()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        private static System.Reflection.Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
        {
            var requestedName = new System.Reflection.AssemblyName(args.Name);
            if (!requestedName.Name?.StartsWith("Siemens.Engineering", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                return null;
            }

            const string registryKey = @"SOFTWARE\Siemens\Automation\_InstalledSoftware\TIAP\21.0";
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryKey);
            if (key != null)
            {
                var installPath = (key.GetValue("Path") as string) ?? (key.GetValue("INSTALLPATH") as string) ?? (key.GetValue("") as string);
                if (!string.IsNullOrEmpty(installPath))
                {
                    // TIA V21 .NET 8 DLL is usually in PublicAPI\V21 folder.
                    var assemblyPath = Path.Combine(installPath, @"PublicAPI\V21", $"{requestedName.Name}.dll");
                    if (File.Exists(assemblyPath))
                    {
                        return System.Reflection.Assembly.LoadFrom(assemblyPath);
                    }
                }
            }

            return null;
        }

        private static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.AddConsole(opts => opts.LogToStandardErrorThreshold = LogLevel.Trace);
            builder.Services.AddSingleton(new ProjectSessionBinding(ResolveStartupProjectPath(args)));
            builder.Services.AddSingleton(WriteSafetyService.Shared);
            builder.Services.AddSingleton<OpennessWorkerClient>();
            builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
            await builder.Build().RunAsync();
        }

        private static string? ResolveStartupProjectPath(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--project", StringComparison.OrdinalIgnoreCase) &&
                    i + 1 < args.Length)
                {
                    return args[i + 1];
                }

                const string projectPrefix = "--project=";
                if (args[i].StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i].Substring(projectPrefix.Length);
                }
            }

            return Environment.GetEnvironmentVariable("TIA_MCP_PROJECT_PATH");
        }
    }
}
