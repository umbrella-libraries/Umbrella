
namespace Umbrella.FileSystem.SharePoint.Test;

public class UmbrellaSharePointFileProviderTest
{
	private const string SiteId = "zinofi.sharepoint.com:/sites/Berkeley:";
	private const string DriveName = "General Document";

	[Fact]
	public void SubPathTranslator_MapsCorrectly()
	{
		var options = new UmbrellaSharePointFileStorageProviderOptions
		{
			SiteId = SiteId,
			DriveName = DriveName,
			GraphServiceClient = null!,
			SubPathTranslator = BuildSubPathTranslator(),
			SubPathReverseTranslator = BuildSubPathReverseTranslator()
		};

		Assert.Equal(
			"General Documents_aff05f67d734f11188b57c1e522f456d/my-file.docx",
			options.SubPathTranslator!("/files/profile-general-documents/aff05f67d734f11188b57c1e522f456d/my-file.docx"));

		Assert.Equal(
			"General Documents_aff05f67d734f11188b57c1e522f456d",
			options.SubPathTranslator("/files/profile-general-documents/aff05f67d734f11188b57c1e522f456d"));
	}

	[Fact]
	public void SubPathReverseTranslator_MapsCorrectly()
	{
		var options = new UmbrellaSharePointFileStorageProviderOptions
		{
			SiteId = SiteId,
			DriveName = DriveName,
			GraphServiceClient = null!,
			SubPathTranslator = BuildSubPathTranslator(),
			SubPathReverseTranslator = BuildSubPathReverseTranslator()
		};

		string spPath = "General Documents_aff05f67d734f11188b57c1e522f456d/my-file.docx";
		string expected = "/files/profile-general-documents/aff05f67d734f11188b57c1e522f456d/my-file.docx";

		Assert.Equal(expected, options.SubPathReverseTranslator!(spPath));
	}

	private static Func<string, string> BuildSubPathTranslator() => path =>
	{
		string[] parts = path.TrimStart('/').Split('/');

		if (parts.Length >= 3 && parts[0] == "files" && parts[1] == "profile-general-documents")
		{
			string folderName = $"General Documents_{parts[2]}";
			return parts.Length > 3 ? $"{folderName}/{string.Join("/", parts[3..])}" : folderName;
		}

		return path.TrimStart('/');
	};

	private static Func<string, string> BuildSubPathReverseTranslator() => spPath =>
	{
		const string prefix = "General Documents_";

		if (spPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			string withoutPrefix = spPath[prefix.Length..];
			int slashIndex = withoutPrefix.IndexOf('/', StringComparison.Ordinal);

			return slashIndex >= 0
				? $"/files/profile-general-documents/{withoutPrefix[..slashIndex]}/{withoutPrefix[(slashIndex + 1)..]}"
				: $"/files/profile-general-documents/{withoutPrefix}";
		}

		return "/" + spPath;
	};
}
