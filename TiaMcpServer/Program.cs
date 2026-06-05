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

            // Probe versions from newest to oldest
            foreach (var version in new[] { 21, 19, 18, 16 })
            {
                // Registry Pattern 1: InstalledApps
                var installedAppsKey = $@"SOFTWARE\Siemens\Automation\InstalledApps\Totally Integrated Automation Portal V{version}";
                var assembly = ProbeRegistryKey(installedAppsKey, "INSTALLPATH", requestedName.Name!, version);
                if (assembly is not null)
                {
                    return assembly;
                }

                // Registry Pattern 2: _InstalledSoftware
                var installedSoftwareKey = $@"SOFTWARE\Siemens\Automation\_InstalledSoftware\TIAP\{version}.0";
                assembly = ProbeRegistryKey(installedSoftwareKey, "Path", requestedName.Name!, version);
                if (assembly is not null)
                {
                    return assembly;
                }
            }

            return null;
        }

        private static System.Reflection.Assembly? ProbeRegistryKey(string subKey, string valueName, string assemblyName, int version)
        {
            foreach (var registryView in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
            {
                using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, registryView);
                using var key = baseKey.OpenSubKey(subKey);
                if (key is null)
                {
                    continue;
                }

                var installPath = (key.GetValue(valueName) as string)
                    ?? (key.GetValue("INSTALLPATH") as string)
                    ?? (key.GetValue("Path") as string)
                    ?? (key.GetValue("") as string);
                if (string.IsNullOrEmpty(installPath))
                {
                    continue;
                }

                // V21+: DLLs are in PublicAPI\V21\net48
                var subDir = version >= 21 ? $@"PublicAPI\V{version}\net48" : $@"PublicAPI\V{version}";
                var assemblyPath = Path.Combine(installPath, subDir, $"{assemblyName}.dll");
                if (File.Exists(assemblyPath))
                {
                    return System.Reflection.Assembly.LoadFrom(assemblyPath);
                }

                // Also try without net48 subfolder for V21+
                if (version >= 21)
                {
                    assemblyPath = Path.Combine(installPath, $@"PublicAPI\V{version}", $"{assemblyName}.dll");
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
