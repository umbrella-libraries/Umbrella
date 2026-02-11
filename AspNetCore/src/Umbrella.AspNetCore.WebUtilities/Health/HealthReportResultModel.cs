using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Umbrella.AspNetCore.WebUtilities.Health;

[System.Diagnostics.CodeAnalysis.SuppressMessage("System.Text.Json.SourceGeneration", "SYSLIB1037:Deserialization of init-only properties is currently not supported in source generation mode.", Justification = "Deserialization not required.")]
internal readonly record struct HealthReportResultModel(
	string Name,
	[property: JsonConverter(typeof(JsonStringEnumConverter))] HealthStatus Status,
	string? Description,
	Dictionary<string, string?> Data);