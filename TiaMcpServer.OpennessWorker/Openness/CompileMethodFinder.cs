using System;
using System.Reflection;

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
