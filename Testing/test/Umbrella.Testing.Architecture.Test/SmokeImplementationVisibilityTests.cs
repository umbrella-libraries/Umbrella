using System.Reflection;

namespace Umbrella.Testing.Architecture.Test;

public sealed class SmokeImplementationVisibilityTests : UmbrellaImplementationVisibilityTests
{
    protected override Assembly CoreLogic => typeof(SmokeImplementationVisibilityTests).Assembly;
}

internal abstract class ReusableService;

internal abstract class ReusableFileHandler;

internal abstract class ReusableAuthorizationHandler;

internal sealed class ConcreteService;

internal sealed class ConcreteFileHandler;

internal sealed class ConcreteAuthorizationHandler;
