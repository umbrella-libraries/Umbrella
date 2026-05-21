using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Umbrella.Testing.Architecture;

public abstract class UmbrellaLayerDependencyTests
{
    protected abstract string NamespacePrefix { get; }
    protected abstract Assembly CoreDomain { get; }
    protected virtual Assembly? CoreData => null;
    protected abstract Assembly CoreLogic { get; }
    protected virtual Assembly? WebServerModels => null;
    protected virtual Assembly? WebServerModelFactories => null;
    protected virtual Assembly? WebShared => null;
    protected virtual Assembly? WebClientData => null;

    [Fact]
    public void CoreDomain_DoesNotDependOnCoreData()
    {
        if (CoreData is null)
            Assert.Skip("CoreData assembly not registered for this project.");

        AssertNoDependency(CoreDomain, $"{NamespacePrefix}.Core.Data");
    }

    [Fact]
    public void CoreDomain_DoesNotDependOnCoreLogic() =>
        AssertNoDependency(CoreDomain, $"{NamespacePrefix}.Core.Logic");

    [Fact]
    public void CoreDomain_DoesNotDependOnWebLayer() =>
        AssertNoDependency(CoreDomain, $"{NamespacePrefix}.Web");

    [Fact]
    public void CoreData_DoesNotDependOnCoreLogic()
    {
        if (CoreData is null)
            Assert.Skip("CoreData assembly not registered for this project.");

        AssertNoDependency(CoreData, $"{NamespacePrefix}.Core.Logic");
    }

    [Fact]
    public void CoreData_DoesNotDependOnWebLayer()
    {
        if (CoreData is null)
            Assert.Skip("CoreData assembly not registered for this project.");

        AssertNoDependency(CoreData, $"{NamespacePrefix}.Web");
    }

    [Fact]
    public void CoreLogic_DoesNotDependOnWebLayer() =>
        AssertNoDependency(CoreLogic, $"{NamespacePrefix}.Web");

    [Fact]
    public void WebServerModels_DoesNotDependOnCoreLogic()
    {
        if (WebServerModels is null)
            Assert.Skip("WebServerModels assembly not registered for this project.");

        AssertNoDependency(WebServerModels, $"{NamespacePrefix}.Core.Logic");
    }

    [Fact]
    public void WebServerModels_DoesNotDependOnCoreData()
    {
        if (WebServerModels is null)
            Assert.Skip("WebServerModels assembly not registered for this project.");

        AssertNoDependency(WebServerModels, $"{NamespacePrefix}.Core.Data");
    }

    [Fact]
    public void WebShared_DoesNotDependOnCore()
    {
        if (WebShared is null)
            Assert.Skip("WebShared assembly not registered for this project.");

        AssertNoDependency(WebShared, $"{NamespacePrefix}.Core");
    }

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
