using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

public static class AssemblyResolver
{
    private static TiaVersionInfo? _detectedVersion;
    private static Assembly? _singleDllCache;
    private static readonly object _lock = new();

    public static TiaVersionInfo? DetectedVersion => _detectedVersion;

    public static void Register()
    {
        // Check for preferred version from environment variable
        int? preferredVersion = null;
        var envVersion = Environment.GetEnvironmentVariable("TIA_PREFERRED_VERSION");
        if (!string.IsNullOrWhiteSpace(envVersion) && int.TryParse(envVersion, out var parsed))
        {
            preferredVersion = parsed;
            Console.Error.WriteLine($"TIA preferred version from env: V{parsed}");
        }

        _detectedVersion = TiaVersionDetector.DetectInstalledVersion(preferredVersion);

        if (_detectedVersion is not null)
        {
            Console.Error.WriteLine(
                $"TIA Portal detected: {_detectedVersion.DisplayName} " +
                $"(DLLs: {_detectedVersion.DllDirectory}, SplitDlls: {_detectedVersion.UsesSplitDlls})");
        }
        else
        {
            Console.Error.WriteLine("No TIA Portal installation detected.");
        }

        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
    }

    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var requestedName = new AssemblyName(args.Name);

        if (requestedName.Name is null ||
            !requestedName.Name.StartsWith("Siemens.Engineering", StringComparison.Ordinal))
        {
            return null;
        }

        if (_detectedVersion is null || _detectedVersion.DllDirectory is null)
        {
            return null;
        }

        if (_detectedVersion.UsesSplitDlls)
        {
            // V21+: Load split DLLs directly by name
            var assemblyPath = Path.Combine(_detectedVersion.DllDirectory, $"{requestedName.Name}.dll");
            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }

            return null;
        }

        // V16-V19: Single Siemens.Engineering.dll
        // Only resolve the main 'Siemens.Engineering' assembly ourselves.
        // Sub-assemblies like 'Siemens.Engineering.Contract' are resolved by the GAC
        // (installed by TIA Portal). Redirecting them to the single DLL fails because
        // the manifest doesn't match (different assembly name/version/public key).
        if (!string.Equals(requestedName.Name, "Siemens.Engineering", StringComparison.Ordinal))
        {
            // Let the runtime fall through to GAC / default probing for sub-assemblies
            return null;
        }

        // Legacy workers are compiled against a specific version's DLL. Always load that
        // version's DLL for type compatibility, regardless of which TIA version we detect.
        // The TIA_PREFERRED_VERSION env var only controls which process to attach to.
#if LEGACY_TIA_V16
        const string legacyDllDirectory = @"C:\Program Files\Siemens\Automation\Portal V16\PublicAPI\V16";
        Console.Error.WriteLine($"[AssemblyResolver] V16 worker — loading V16 DLL from {legacyDllDirectory}");
#elif LEGACY_TIA
        const string legacyDllDirectory = @"C:\Program Files\Siemens\Automation\Portal V18\PublicAPI\V18";
        Console.Error.WriteLine($"[AssemblyResolver] Legacy worker — loading V18 DLL from {legacyDllDirectory}");
#else
        var legacyDllDirectory = _detectedVersion.DllDirectory;
        Console.Error.WriteLine($"[AssemblyResolver] Loading single Siemens.Engineering.dll for V{_detectedVersion.MajorVersion}");
#endif

        // Return the cached single DLL for the main Siemens.Engineering request
        lock (_lock)
        {
            if (_singleDllCache is not null)
            {
                return _singleDllCache;
            }

            var singleDllPath = Path.Combine(legacyDllDirectory, "Siemens.Engineering.dll");
            if (File.Exists(singleDllPath))
            {
                _singleDllCache = Assembly.LoadFrom(singleDllPath);
                return _singleDllCache;
            }
        }

        return null;
    }
}
