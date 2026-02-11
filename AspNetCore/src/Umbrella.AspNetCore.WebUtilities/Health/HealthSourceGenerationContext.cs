using System.Text.Json.Serialization;

namespace Umbrella.AspNetCore.WebUtilities.Health;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HealthReportModel))]
internal partial class HealthSourceGenerationContext : JsonSerializerContext
{
}