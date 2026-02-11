
using System.Xml.Serialization;
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Extensions;

namespace Umbrella.WebUtilities.SiteMap;

/// <summary>
/// A class used to create an XML SiteMap.
/// </summary>
[XmlRoot("urlset", Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9")]
public class XmlSiteMap
{
	/// <summary>
	/// Gets or sets the urls.
	/// </summary>
	[XmlElement("url")]
	public List<XmlSiteMapUrl> Urls { get; } = [];

	/// <summary>
	/// Adds the specified URL.
	/// </summary>
	/// <param name="url">The URL.</param>
	/// <param name="changeFrequency">The change frequency.</param>
	/// <param name="lastModified">The last modified.</param>
	/// <param name="priority">The priority.</param>
	/// <returns>The <see cref="XmlSiteMap"/>.</returns>
	public XmlSiteMap Add(string url, ChangeFrequency? changeFrequency = null, DateTime? lastModified = null, double? priority = null)
	{
		Guard.IsNotNullOrWhiteSpace(url);

		url = url.TrimToLowerInvariant();

		// Ensure the list doesn't already include this URL
		if (!Urls.Any(x => x.Url == url))
		{
			Urls.Add(new XmlSiteMapUrl
			{
				Url = url,
				ChangeFrequency = changeFrequency,
				LastModified = lastModified.HasValue ? lastModified.Value.ToUniversalTime().ToString("o") : "",
				Priority = priority
			});
		}

		return this;
	}

	/// <inheritdoc />
	public override string ToString()
	{
		using var ms = new MemoryStream();
		var xs = new XmlSerializer(typeof(XmlSiteMap));
		xs.Serialize(ms, this);

		ms.Position = 0;

		using var reader = new StreamReader(ms);
		return reader.ReadToEnd();
	}
}