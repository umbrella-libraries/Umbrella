using System.Reflection;

namespace Umbrella.Testing.Architecture.Test;

public sealed class SmokeImplementationVisibilityTests : UmbrellaImplementationVisibilityTests
{
    protected override Assembly CoreLogic => typeof(UmbrellaLayerDependencyTests).Assembly;
}
