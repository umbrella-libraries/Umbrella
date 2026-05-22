using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Umbrella.Testing.Architecture;

/// <summary>
/// Abstract base class providing xunit.v3 tests that enforce layer dependency direction
/// across the standard Umbrella project structure.
/// </summary>
/// <remarks>
/// Inherit from this class and override the abstract properties to supply the assembly
/// for each layer. Optional layers (<see cref="CoreData"/>, <see cref="WebServerModels"/>,
/// <see cref="WebServerModelFactories"/>, <see cref="WebShared"/>, <see cref="WebClientData"/>)
/// default to <see langword="null"/>; tests for absent layers are reported as skipped rather
/// than failed.
/// </remarks>
public abstract class UmbrellaLayerDependencyTests
{
    /// <summary>Gets the root namespace prefix for the project (e.g. <c>"IndyRecords"</c>).</summary>
    protected abstract string NamespacePrefix { get; }

    /// <summary>Gets the assembly for the <c>Core.Domain</c> layer.</summary>
    protected abstract Assembly CoreDomain { get; }

    /// <summary>
    /// Gets the assembly for the <c>Core.Data</c> layer, or <see langword="null"/> if the project
    /// has no data layer (e.g. Optimizely sites that rely on Remarkable.Optimizely packages).
    /// </summary>
    protected virtual Assembly? CoreData => null;

    /// <summary>Gets the assembly for the <c>Core.Logic</c> layer.</summary>
    protected abstract Assembly CoreLogic { get; }

    /// <summary>
    /// Gets the assembly for the <c>Web.Server.Models</c> layer, or <see langword="null"/> if absent.
    /// </summary>
    protected virtual Assembly? WebServerModels => null;

    /// <summary>
    /// Gets the assembly for the <c>Web.Server.ModelFactories</c> layer, or <see langword="null"/> if absent.
    /// </summary>
    protected virtual Assembly? WebServerModelFactories => null;

    /// <summary>
    /// Gets the assembly for the <c>Web.Shared</c> layer, or <see langword="null"/> if absent.
    /// </summary>
    protected virtual Assembly? WebShared => null;

    /// <summary>
    /// Gets the assembly for the <c>Web.Client.Data</c> layer, or <see langword="null"/> if absent.
    /// </summary>
    protected virtual Assembly? WebClientData => null;

    /// <summary>Verifies that <c>Core.Domain</c> does not reference <c>Core.Data</c>.</summary>
    [Fact]
    public void CoreDomain_DoesNotDependOnCoreData()
    {
        if (CoreData is null)
            Assert.Skip("CoreData assembly not registered for this project.");

        AssertNoDependency(CoreDomain, $"{NamespacePrefix}.Core.Data");
    }

    /// <summary>Verifies that <c>Core.Domain</c> does not reference <c>Core.Logic</c>.</summary>
    [Fact]
    public void CoreDomain_DoesNotDependOnCoreLogic() =>
        AssertNoDependency(CoreDomain, $"{NamespacePrefix}.Core.Logic");

    /// <summary>Verifies that <c>Core.Domain</c> does not reference any <c>Web</c> layer.</summary>
    [Fact]
    public void CoreDomain_DoesNotDependOnWebLayer() =>
        AssertNoDependency(CoreDomain, $"{NamespacePrefix}.Web");

    /// <summary>Verifies that <c>Core.Data</c> does not reference <c>Core.Logic</c>.</summary>
    [Fact]
    public void CoreData_DoesNotDependOnCoreLogic()
    {
        if (CoreData is null)
            Assert.Skip("CoreData assembly not registered for this project.");

        AssertNoDependency(CoreData, $"{NamespacePrefix}.Core.Logic");
    }

    /// <summary>Verifies that <c>Core.Data</c> does not reference any <c>Web</c> layer.</summary>
    [Fact]
    public void CoreData_DoesNotDependOnWebLayer()
    {
        if (CoreData is null)
            Assert.Skip("CoreData assembly not registered for this project.");

        AssertNoDependency(CoreData, $"{NamespacePrefix}.Web");
    }

    /// <summary>Verifies that <c>Core.Logic</c> does not reference any <c>Web</c> layer.</summary>
    [Fact]
    public void CoreLogic_DoesNotDependOnWebLayer() =>
        AssertNoDependency(CoreLogic, $"{NamespacePrefix}.Web");

    /// <summary>Verifies that <c>Web.Server.Models</c> does not reference <c>Core.Logic</c>.</summary>
    [Fact]
    public void WebServerModels_DoesNotDependOnCoreLogic()
    {
        if (WebServerModels is null)
            Assert.Skip("WebServerModels assembly not registered for this project.");

        AssertNoDependency(WebServerModels, $"{NamespacePrefix}.Core.Logic");
    }

    /// <summary>Verifies that <c>Web.Server.Models</c> does not reference <c>Core.Data</c>.</summary>
    [Fact]
    public void WebServerModels_DoesNotDependOnCoreData()
    {
        if (WebServerModels is null)
            Assert.Skip("WebServerModels assembly not registered for this project.");

        AssertNoDependency(WebServerModels, $"{NamespacePrefix}.Core.Data");
    }

    /// <summary>Verifies that <c>Web.Shared</c> does not reference any <c>Core</c> layer.</summary>
    [Fact]
    public void WebShared_DoesNotDependOnCore()
    {
        if (WebShared is null)
            Assert.Skip("WebShared assembly not registered for this project.");

        AssertNoDependency(WebShared, $"{NamespacePrefix}.Core");
    }

    /// <summary>Verifies that <c>Web.Client.Data</c> does not reference any <c>Core</c> layer.</summary>
    [Fact]
    public void WebClientData_DoesNotDependOnCore()
    {
        if (WebClientData is null)
            Assert.Skip("WebClientData assembly not registered for this project.");

        AssertNoDependency(WebClientData, $"{NamespacePrefix}.Core");
    }

    private static void AssertNoDependency(Assembly assembly, string forbiddenNamespace)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Failing types:{Environment.NewLine}{string.Join(Environment.NewLine, result.FailingTypeNames ?? [])}");
    }
}
