using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Reflection discovery of an Openness object's Compile method — the exact discovery order
/// compile_check uses, extracted so it is unit-testable: (1) the type itself, public AND
/// non-public (explicit interface implementations are private), (2) implemented interfaces,
/// (3) the base-type chain (COM wrappers can hide Compile in a base class — the shape that
/// made the cross-reference auto-compile fail with "PlcSoftware does not expose a Compile
/// method" while compile_check on the same object worked).
/// </summary>
internal static class CompileMethodFinder
{
    /// <summary>
    /// Resolve an Openness service via the object's generic <c>GetService&lt;T&gt;()</c> method,
    /// by reflection. This is the REAL compile route on V16/V18: <c>PlcSoftware</c> (and most
    /// other compilables) declare no <c>Compile</c> method on the type at all — the compiler
    /// is obtained as a service, <c>GetService&lt;ICompilable&gt;()</c>. Reflection (rather than
    /// a direct call) keeps this Siemens-free and version-proof: every Openness object
    /// declares GetService on itself, but WHERE it is declared differs between the single-DLL
    /// (V16/V18) and split-DLL (V21+) wrappers. Returns null when the object has no generic
    /// GetService or the service is unavailable; an exception thrown BY GetService is
    /// unwrapped and rethrown as itself.
    /// </summary>
    public static object? TryGetService(object target, Type serviceType)
    {
        var getService = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == "GetService"
                                 && m.IsGenericMethodDefinition
                                 && m.GetGenericArguments().Length == 1);
        if (getService is null)
        {
            return null;
        }

        try
        {
            return getService.MakeGenericMethod(serviceType).Invoke(target, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    public static MethodInfo? Find(Type type)
    {
        const BindingFlags allFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // 1. Search the type itself (public and non-public — explicit interface implementations are private)
        var compileMethod = type.GetMethod("Compile", allFlags);
        if (compileMethod != null)
        {
            return compileMethod;
        }

        // 2. Search all implemented interfaces
        foreach (var interfaceType in type.GetInterfaces())
        {
            compileMethod = interfaceType.GetMethod("Compile", BindingFlags.Instance | BindingFlags.Public);
            if (compileMethod != null)
            {
                return compileMethod;
            }
        }

        // 3. Walk base types (COM wrappers may hide Compile in a base class)
        var baseType = type.BaseType;
        while (baseType is not null)
        {
            compileMethod = baseType.GetMethod("Compile", allFlags);
            if (compileMethod != null)
            {
                return compileMethod;
            }

            baseType = baseType.BaseType;
        }

        return null;
    }
}
