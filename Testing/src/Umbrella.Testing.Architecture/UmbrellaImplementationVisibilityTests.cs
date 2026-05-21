using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Umbrella.Testing.Architecture;

public abstract class UmbrellaImplementationVisibilityTests
{
    protected virtual Assembly? CoreData => null;
    protected abstract Assembly CoreLogic { get; }

    [Fact]
    public void CoreData_Repositories_AreInternalSealed()
    {
        if (CoreData is null)
            Assert.Skip("CoreData assembly not registered for this project.");

        AssertInternalSealed(CoreData, "Repository$");
    }

    [Fact]
    public void CoreLogic_Services_AreInternalSealed() =>
        AssertInternalSealed(CoreLogic, "Service$");

    [Fact]
    public void CoreLogic_FileHandlers_AreInternalSealed() =>
        AssertInternalSealed(CoreLogic, "FileHandler$");

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
