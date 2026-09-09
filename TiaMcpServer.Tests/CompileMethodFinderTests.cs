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

    // --- TryGetService: the V16/V18 compile route -------------------------------------------
    // The real V18 PlcSoftware declares NO Compile method — the compiler is obtained as a
    // service via the object's generic GetService<T>(). These pin the reflection helper
    // compile_check relies on before it ever falls back to the Compile-method hunt.

    private interface IPingService
    {
        string Ping();
    }

    private sealed class PingService : IPingService
    {
        public string Ping() => "pong";
    }

    private sealed class HasGenericGetService
    {
        public T? GetService<T>() where T : class
            => typeof(T) == typeof(IPingService) ? (T)(object)new PingService() : null;
    }

    private sealed class GetServiceForAnything
    {
        public T GetService<T>() where T : class => (T)Activator.CreateInstance(typeof(T))!;
    }

    private sealed class NoGetServiceAtAll
    {
        public int Value => 1;
    }

    private sealed class GetServiceThrows
    {
        public T GetService<T>() where T : class => throw new InvalidOperationException("no service");
    }

    [Fact]
    public void TryGetService_Resolves_The_Requested_Service()
    {
        var service = CompileMethodFinder.TryGetService(new HasGenericGetService(), typeof(IPingService));

        var ping = Assert.IsAssignableFrom<IPingService>(service);
        Assert.Equal("pong", ping.Ping());
    }

    [Fact]
    public void TryGetService_Returns_Null_When_No_Generic_GetService_Exists()
    {
        Assert.Null(CompileMethodFinder.TryGetService(new NoGetServiceAtAll(), typeof(IPingService)));
    }

    [Fact]
    public void TryGetService_Returns_Null_When_The_Service_Is_Unavailable()
    {
        // GetService<T> exists but returns null for the requested marker → caller must fall
        // through to the Compile-method routes, not treat this as "compilable"
        Assert.Null(CompileMethodFinder.TryGetService(new HasGenericGetService(), typeof(IDisposable)));
    }

    [Fact]
    public void TryGetService_Unwraps_The_Inner_Exception_Thrown_By_GetService()
    {
        // an EngineeringException from Openness must surface as itself (so callers' catch
        // filters match), never wrapped in TargetInvocationException
        var ex = Assert.Throws<InvalidOperationException>(
            () => CompileMethodFinder.TryGetService(new GetServiceThrows(), typeof(IPingService)));
        Assert.Equal("no service", ex.Message);
    }

    [Fact]
    public void TryGetService_Closes_Over_The_Requested_Marker_Type()
    {
        // the generic method must be closed with the SERVICE type, not the target's type —
        // a wrong closure silently resolves the wrong service
        var service = CompileMethodFinder.TryGetService(new GetServiceForAnything(), typeof(PingService));
        Assert.IsType<PingService>(service);
    }
}
