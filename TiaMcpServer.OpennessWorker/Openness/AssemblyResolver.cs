using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class AssemblyResolver
{
    private const string TiaPortalV21DirEnvironmentVariable = "TiaPortalV21Dir";
    private const string TiaPortalV21RegistrySubKey =
        @"SOFTWARE\Siemens\Automation\InstalledApps\Totally Integrated Automation Portal V21";
    private const string StandardOpennessInstallPath =
        @"C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48";

    private static readonly string[] RequiredAssemblies =
    {
        "Siemens.Engineering.Base.dll",
        "Siemens.Engineering.Step7.dll"
    };

    public static void Register()
    {
        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
    }

    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var requestedName = new AssemblyName(args.Name);

        if (requestedName.Name is null ||
            !requestedName.Name.StartsWith("Siemens.Engineering.", StringComparison.Ordinal))
        {
            return null;
        }

        var opennessInstallPath = GetOpennessInstallPath();
        var assemblyPath = Path.Combine(opennessInstallPath, $"{requestedName.Name}.dll");

        if (!File.Exists(assemblyPath))
        {
            return null;
        }

        var loadedAssembly = Assembly.LoadFrom(assemblyPath);

        if (!string.Equals(requestedName.FullName, loadedAssembly.GetName().FullName, StringComparison.Ordinal))
        {
            throw new FileNotFoundException("TIA Portal Openness version does not match the requested assembly.", assemblyPath);
        }

        return loadedAssembly;
    }

    private static string GetOpennessInstallPath()
    {
        var checkedLocations = new List<string>();

        var environmentPath = Environment.GetEnvironmentVariable(TiaPortalV21DirEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            var candidatePath = environmentPath.Trim().Trim('"');
            checkedLocations.Add($"{TiaPortalV21DirEnvironmentVariable}: {candidatePath}");

            if (ContainsRequiredAssemblies(candidatePath))
            {
                return candidatePath;
            }
        }

        foreach (var registryView in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            var registryPath = $@"HKLM\{TiaPortalV21RegistrySubKey} ({registryView})";
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
            using var key = baseKey.OpenSubKey(TiaPortalV21RegistrySubKey);

            if (key is null)
            {
                checkedLocations.Add($"{registryPath}: key not found");
                continue;
            }

            var installPath = key.GetValue("INSTALLPATH") as string;

            if (string.IsNullOrWhiteSpace(installPath))
            {
                checkedLocations.Add($"{registryPath}: INSTALLPATH not set");
                continue;
            }

            var candidatePath = Path.Combine(installPath, @"PublicAPI\V21\net48");
            checkedLocations.Add($"{registryPath}: {candidatePath}");

            if (ContainsRequiredAssemblies(candidatePath))
            {
                return candidatePath;
            }
        }

        checkedLocations.Add(StandardOpennessInstallPath);
        if (ContainsRequiredAssemblies(StandardOpennessInstallPath))
        {
            return StandardOpennessInstallPath;
        }

        throw new FileNotFoundException(
            "TIA Portal V21 Openness assemblies were not found. Checked locations: " +
            string.Join("; ", checkedLocations) +
            $". Set {TiaPortalV21DirEnvironmentVariable} to the folder containing Siemens.Engineering.*.dll files.");
    }

    private static bool ContainsRequiredAssemblies(string directoryPath)
    {
        return Directory.Exists(directoryPath) &&
            RequiredAssemblies.All(assemblyName => File.Exists(Path.Combine(directoryPath, assemblyName)));
    }
}
