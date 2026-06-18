using Umbrella.Utilities.Helpers;

namespace Umbrella.AspNetCore.WebUtilities.Test.Hosting;

public class UmbrellaWebHostingEnvironmentTest
{
	[Fact]
	public void MapPath_InvalidPath()
	{
		var env = Mocks.CreateUmbrellaWebHostingEnvironment();

#nullable disable
		_ = Assert.Throws<ArgumentNullException>(() => env.MapPath(null));
#nullable enable
		_ = Assert.Throws<ArgumentException>(() => env.MapPath(""));
		_ = Assert.Throws<ArgumentException>(() => env.MapPath("      "));
	}

	[Fact]
	public void MapPath_VirtualPath_ContentRoot()
	{
		string path = "~/images/test.jpg";

		var env = Mocks.CreateUmbrellaWebHostingEnvironment();

		string mappedPath = env.MapPath(path);

		Assert.Equal(PathHelper.PlatformNormalize(@"C:\MockedWebApp\src\images\test.jpg"), mappedPath);
	}

	[Fact]
	public void MapPath_VirtualPath_WebRoot()
	{
		string path = "~/images/test.jpg";

		var env = Mocks.CreateUmbrellaWebHostingEnvironment();

		string mappedPath = env.MapPath(path, false);

		Assert.Equal(PathHelper.PlatformNormalize(@"C:\MockedWebApp\src\wwwroot\images\test.jpg"), mappedPath);
	}

	[Fact]
	public void MapPath_RelativePath_ContentRoot()
	{
		string path = "/images/test.jpg";

		var env = Mocks.CreateUmbrellaWebHostingEnvironment();

		string mappedPath = env.MapPath(path);

		Assert.Equal(PathHelper.PlatformNormalize(@"C:\MockedWebApp\src\images\test.jpg"), mappedPath);
	}

	[Fact]
	public void MapPath_RelativePath_WebRoot()
	{
		string path = "/images/test.jpg";

		var env = Mocks.CreateUmbrellaWebHostingEnvironment();

		string mappedPath = env.MapPath(path, false);

		Assert.Equal(PathHelper.PlatformNormalize(@"C:\MockedWebApp\src\wwwroot\images\test.jpg"), mappedPath);
	}

	[Fact]
	public void MapWebPath_VirtualPath()
	{
		string path = "~/images/test.jpg";

		var env = Mocks.CreateUmbrellaWebHostingEnvironment();

		string mappedPath = env.MapWebPath(path, true);

		Assert.Equal("https://www.test.com/images/test.jpg", mappedPath);
	}

	[Fact]
	public void MapWebPath_RelativePath()
	{
		string path = "/images/test.jpg";

		var env = Mocks.CreateUmbrellaWebHostingEnvironment();

		string mappedPath = env.MapWebPath(path, true);

		Assert.Equal("https://www.test.com/images/test.jpg", mappedPath);
	}
}