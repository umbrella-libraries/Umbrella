using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Umbrella.Testing.Architecture;

/// <summary>
/// Abstract base class providing xunit.v3 tests that enforce <c>internal sealed</c> visibility
/// on all concrete repository, service, file-handler, and authorization-handler implementations.
/// </summary>
/// <remarks>
/// Inherit from this class and override the abstract <see cref="CoreLogic"/> property.
/// Override <see cref="CoreData"/> if the project has a data layer; tests for absent layers
/// are reported as skipped rather than failed.
/// </remarks>
public abstract class UmbrellaImplementationVisibilityTests
{
    /// <summary>
    /// Gets the assembly for the <c>Core.Data</c> layer, or <see langword="null"/> if the project
    /// has no data layer (e.g. Optimizely sites that rely on Remarkable.Optimizely packages).
    /// </summary>
    protected virtual Assembly? CoreData => null;

    /// <summary>Gets the assembly for the <c>Core.Logic</c> layer.</summary>
    protected abstract Assembly CoreLogic { get; }

    /// <summary>Verifies that all classes named <c>*Repository</c> in <c>Core.Data</c> are <c>internal sealed</c>.</summary>
    [Fact]
    public void CoreData_Repositories_AreInternalSealed()
    {
        if (CoreData is null)
            Assert.Skip("CoreData assembly not registered for this project.");

        AssertInternalSealed(CoreData, "Repository$");
    }

    /// <summary>Verifies that all classes named <c>*Service</c> in <c>Core.Logic</c> are <c>internal sealed</c>.</summary>
    [Fact]
    public void CoreLogic_Services_AreInternalSealed() =>
        AssertInternalSealed(CoreLogic, "Service$");

    /// <summary>Verifies that all classes named <c>*FileHandler</c> in <c>Core.Logic</c> are <c>internal sealed</c>.</summary>
    [Fact]
    public void CoreLogic_FileHandlers_AreInternalSealed() =>
        AssertInternalSealed(CoreLogic, "FileHandler$");

    /// <summary>Verifies that all classes named <c>*AuthorizationHandler</c> in <c>Core.Logic</c> are <c>internal sealed</c>.</summary>
    [Fact]
    public void CoreLogic_AuthorizationHandlers_AreInternalSealed() =>
        AssertInternalSealed(CoreLogic, "AuthorizationHandler$");

    private static void AssertInternalSealed(Assembly assembly, string namePattern)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(assembly)
            .That().AreClasses().And().HaveNameMatching(namePattern)
            .Should().BeSealed().And().NotBePublic()
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Failing types:{Environment.NewLine}{string.Join(Environment.NewLine, result.FailingTypeNames ?? [])}");
    }
}
