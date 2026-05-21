using System.Reflection;

namespace Umbrella.Testing.Architecture.Test;

public sealed class SmokeLayerDependencyTests : UmbrellaLayerDependencyTests
{
    protected override string NamespacePrefix => "Nonexistent.Namespace";
    protected override Assembly CoreDomain => typeof(UmbrellaLayerDependencyTests).Assembly;
    protected override Assembly CoreLogic => typeof(UmbrellaLayerDependencyTests).Assembly;
}
