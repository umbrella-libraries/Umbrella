using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Umbrella.Testing.AspNetCore;

internal sealed class UmbrellaTestWebHostEnvironment : IWebHostEnvironment
{
	private readonly IWebHostEnvironment _inner;

	public UmbrellaTestWebHostEnvironment(IWebHostEnvironment inner, string environmentName)
	{
		_inner = inner;
		EnvironmentName = environmentName;
	}

	public string EnvironmentName { get; set; }

	public string ApplicationName
	{
		get => _inner.ApplicationName;
		set => _inner.ApplicationName = value;
	}

	public string ContentRootPath
	{
		get => _inner.ContentRootPath;
		set => _inner.ContentRootPath = value;
	}

	public IFileProvider ContentRootFileProvider
	{
		get => _inner.ContentRootFileProvider;
		set => _inner.ContentRootFileProvider = value;
	}

	public string WebRootPath
	{
		get => _inner.WebRootPath;
		set => _inner.WebRootPath = value;
	}

	public IFileProvider WebRootFileProvider
	{
		get => _inner.WebRootFileProvider;
		set => _inner.WebRootFileProvider = value;
	}
}
