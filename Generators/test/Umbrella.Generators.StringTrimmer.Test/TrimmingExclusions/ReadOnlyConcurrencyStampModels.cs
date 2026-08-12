using Umbrella.Utilities.Data.Concurrency;
using Umbrella.Utilities.Data.Models;
using Umbrella.Utilities.Text;

namespace Umbrella.Generators.StringTrimmer.Test.TrimmingExclusions;

/// <summary>
/// Reaches <c>ConcurrencyStamp</c> through <see cref="IUpdateResultModel"/>, which derives the read-only
/// <see cref="IReadOnlyConcurrencyStamp"/> contract rather than the mutable <see cref="IConcurrencyStamp"/>.
/// </summary>
/// <remarks>
/// This is the regression guard for the inherited-interface walk in <c>ModelStandardsSymbolAnalysis</c>. Matching only
/// the members declared directly on <c>IConcurrencyStamp</c> would fail to recognise this stamp and the generator would
/// emit a trim statement for it, silently corrupting a value that must round-trip byte-exact.
/// </remarks>
public partial record UpdateResultStampModel : IUmbrellaTrimmable, IUpdateResultModel
{
	public string Name { get; set; } = "";

	public string ConcurrencyStamp { get; set; } = "";
}

/// <summary>
/// Implements the read-only concurrency stamp contract directly, with a mutable property.
/// </summary>
public partial record DirectReadOnlyStampModel : IUmbrellaTrimmable, IReadOnlyConcurrencyStamp
{
	public string Name { get; set; } = "";

	public string ConcurrencyStamp { get; set; } = "";
}

/// <summary>
/// Declares the stamp using an <see langword="init"/> accessor, which the generator's non-init setter gate excludes
/// independently of the concurrency stamp check.
/// </summary>
public partial record InitOnlyStampModel : IUmbrellaTrimmable, IUpdateResultModel
{
	public string Name { get; set; } = "";

	public required string ConcurrencyStamp { get; init; }
}

/// <summary>
/// Holds a nested property whose type reaches the stamp through the read-only contract. The generator recurses into
/// nested non-system types and re-evaluates the exclusion against the nested type, so that path needs its own guard.
/// </summary>
public partial record NestedStampOwnerModel : IUmbrellaTrimmable
{
	public string OwnerName { get; set; } = "";

	public UpdateResultStampModel Nested { get; set; } = new();
}
