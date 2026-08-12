using Microsoft.CodeAnalysis;

namespace Umbrella.ModelStandards.Analysis;

/// <summary>
/// Symbol analysis shared between the model standards analyzer and the string trimmer source generator.
/// </summary>
/// <remarks>
/// This file is linked into both <c>Umbrella.Analyzers</c> and <c>Umbrella.Generators.StringTrimmer</c> rather than
/// being duplicated, because the two projects must agree exactly on what counts as a concurrency stamp. If they
/// disagree, the generator emits a trim statement for a value the analyzer considers excluded.
/// </remarks>
internal static class ModelStandardsSymbolAnalysis
{
	/// <summary>
	/// The metadata name of the mutable concurrency stamp interface.
	/// </summary>
	/// <remarks>
	/// Only the mutable interface is resolved. Stamps reached through the read-only base interface are matched by
	/// the interface-hierarchy walk in <see cref="ImplementsInterfaceProperty"/>, so no second metadata name is
	/// required and there is no risk of the exclusion failing open when only one of two names resolves.
	/// </remarks>
	public const string ConcurrencyStampInterfaceName = "Umbrella.Utilities.Data.Concurrency.IConcurrencyStamp";

	/// <summary>
	/// Determines whether the specified property implements a property declared by the specified interface, or by any
	/// interface that interface inherits.
	/// </summary>
	/// <param name="propertySymbol">The property declared on the concrete type.</param>
	/// <param name="typeSymbol">The concrete type declaring <paramref name="propertySymbol"/>.</param>
	/// <param name="interfaceSymbol">The interface to test against. When <see langword="null"/> this returns <see langword="false"/>.</param>
	/// <returns><see langword="true"/> if the property implements a member of the interface hierarchy; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// The inherited-interface walk is load bearing and must not be removed. <c>IConcurrencyStamp</c> re-declares
	/// <c>ConcurrencyStamp</c> from <c>IReadOnlyConcurrencyStamp</c> using <see langword="new"/>, which creates two
	/// distinct interface slots. A result model that implements only the read-only contract — directly or via
	/// <c>IUpdateResultModel</c> — fills the base slot and not the derived one, so checking <c>GetMembers()</c> on the
	/// mutable interface alone would not recognise it as a concurrency stamp. Matching stays namespace-exact because it
	/// compares interface member symbols, not property names.
	/// </remarks>
	public static bool ImplementsInterfaceProperty(
		IPropertySymbol propertySymbol,
		INamedTypeSymbol typeSymbol,
		INamedTypeSymbol? interfaceSymbol)
	{
		if (interfaceSymbol is null)
			return false;

		if (DeclaresImplementedProperty(interfaceSymbol, typeSymbol, propertySymbol))
			return true;

		foreach (INamedTypeSymbol inheritedInterface in interfaceSymbol.AllInterfaces)
		{
			if (DeclaresImplementedProperty(inheritedInterface, typeSymbol, propertySymbol))
				return true;
		}

		return false;
	}

	private static bool DeclaresImplementedProperty(
		INamedTypeSymbol interfaceSymbol,
		INamedTypeSymbol typeSymbol,
		IPropertySymbol propertySymbol) =>
		interfaceSymbol.GetMembers()
			.OfType<IPropertySymbol>()
			.Any(interfaceProperty =>
				SymbolEqualityComparer.Default.Equals(
					typeSymbol.FindImplementationForInterfaceMember(interfaceProperty),
					propertySymbol));
}
