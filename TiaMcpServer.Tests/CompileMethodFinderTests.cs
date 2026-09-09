using System;
using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// Unit tests for the pure Compile-method discovery shared by compile_check and the
/// cross-reference auto-compile. The discovery order matters: the OLD xref code searched
/// only the type itself + interfaces, so a Compile hidden in a BASE class (the COM-wrapper
/// shape) threw "PlcSoftware does not expose a Compile method" while compile_check on the
/// same object succeeded — these tests pin every shape the finder must handle.
/// </summary>
public class CompileMethodFinderTests
{
    private interface ICompilable
    {
        int Compile();
    }

    private sealed class CompileOnType
    {
        public int Compile() => 1;
    }

    private sealed class ExplicitInterfaceOnly : ICompilable
    {
        int ICompilable.Compile() => 2;
    }

    private abstract class CompilableBase
    {
        public int Compile() => 1;
    }

    private sealed class CompileOnlyOnBase : CompilableBase
    {
    }

    private abstract class CompilableGrandBase
    {
        public string Compile() => "ok";
    }

    private abstract class MiddleBase : CompilableGrandBase
    {
    }

    private sealed class CompileTwoLevelsUp : MiddleBase
    {
    }

    private sealed class NoCompileAnywhere
    {
        public int Transpile() => 0;
    }

    [Fact]
    public void Finds_Compile_Declared_On_The_Type_Itself()
    {
        var method = CompileMethodFinder.Find(typeof(CompileOnType));

        Assert.NotNull(method);
        Assert.Equal("Compile", method.Name);
        Assert.Equal(typeof(CompileOnType), method.DeclaringType);
    }

    [Fact]
    public void Finds_Explicit_Interface_Implementation_Via_The_Interface()
    {
        // an explicit interface implementation is named 'ICompilable.Compile', so no plain
        // 'Compile' exists on the type — the finder's interface pass must catch it
        var method = CompileMethodFinder.Find(typeof(ExplicitInterfaceOnly));

        Assert.NotNull(method);
        Assert.Equal(typeof(ICompilable), method.DeclaringType);
    }

    [Fact]
    public void Finds_Compile_On_A_Base_Class()
    {
        // THE cross-reference regression shape: nothing on the type or its interfaces,
        // Compile lives in the base class — the old xref reflection missed exactly this
        var method = CompileMethodFinder.Find(typeof(CompileOnlyOnBase));

        Assert.NotNull(method);
        Assert.Equal(typeof(CompilableBase), method.DeclaringType);
    }

    [Fact]
    public void Finds_Compile_Two_Levels_Up_The_Base_Chain()
    {
        var method = CompileMethodFinder.Find(typeof(CompileTwoLevelsUp));

        Assert.NotNull(method);
        Assert.Equal(typeof(CompilableGrandBase), method.DeclaringType);
    }

    [Fact]
    public void Returns_Null_When_No_Compile_Exists_Anywhere()
    {
        Assert.Null(CompileMethodFinder.Find(typeof(NoCompileAnywhere)));
    }

    [Fact]
    public void Returns_Null_For_A_Primitive_Type_With_No_Base_Chain()
    {
        Assert.Null(CompileMethodFinder.Find(typeof(int)));
    }
}
