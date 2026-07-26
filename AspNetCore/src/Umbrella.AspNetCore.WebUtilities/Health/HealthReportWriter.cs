using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Umbrella.AspNetCore.WebUtilities.Health;

/// <summary>
/// Contains methods used to write a <see cref="HealthReport" /> to a <see cref="HttpResponse"/>.
/// </summary>
public static class HealthReportWriter
{
	/// <summary>
	/// Writes the specified report to the <see cref="HttpContext.Response"/> as JSON.
	/// </summary>
	/// <param name="context">The Http Context.</param>
	/// <param name="healthReport">The report.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public static Task WriteResponseAsJsonAsync(HttpContext context, HealthReport healthReport)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(healthReport);

		context.Response.ContentType = "application/json; charset=utf-8";

		var report = new HealthReportModel(
			healthReport.Status,
			healthReport.Entries.Select(x => new HealthReportResultModel(x.Key, x.Value.Status, x.Value.Description, x.Value.Data.ToDictionary(x => x.Key, x => x.Value.ToString()))).ToArray());

		string json = JsonSerializer.Serialize(report, typeof(HealthReportModel), HealthSourceGenerationContext.Default);

		return context.Response.WriteAsync(json, context.RequestAborted);
	}
}
