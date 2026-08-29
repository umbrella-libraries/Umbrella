using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Caching;
using Umbrella.Utilities.Data.Abstractions;

namespace Umbrella.Utilities.Test.Caching;

public class CacheKeyUtilityTest
{
	[Fact]
	public void CreateCacheKey()
	{
		CacheKeyUtility utility = CreateCacheKeyUtility();

		string partialKey = "test:key";

		string key = utility.Create<CacheKeyUtilityTest>(partialKey);

		Assert.Equal($"{typeof(CacheKeyUtilityTest).FullName}:{partialKey}".ToUpperInvariant(), key);
	}

	[Fact]
	public void CreateCacheKey_Parts()
	{
		CacheKeyUtility utility = CreateCacheKeyUtility();
		ReadOnlySpan<string> keyParts = ["part1", "part2", "part3", "part4", "part5"];

		string key = utility.Create<CacheKeyUtilityTest>(keyParts);

		Assert.Equal($"{typeof(CacheKeyUtilityTest).FullName}:{string.Join(":", keyParts.ToArray())}".ToUpperInvariant(), key);
	}

	[Fact]
	public void CreateCacheKey_NonStack()
	{
		CacheKeyUtility utility = CreateCacheKeyUtility();

		string partialKey = "Diam eume rat sed gubergren diam lorem consequat ut accusam ad clita consetetur no vel erat no diam cum nibh no vel erat no diam cum nibh";

		string key = utility.Create<CacheKeyUtilityTest>(partialKey);

		Assert.Equal($"{typeof(CacheKeyUtilityTest).FullName}:{partialKey}".ToUpperInvariant(), key);
	}

	[Fact]
	public void CreateCacheKey_Parts_NonStack()
	{
		CacheKeyUtility utility = CreateCacheKeyUtility();
		string[] keyParts = ["part1", "part2", "part3", "part4", "part5", "part6", "part7", "part8", "part9", "part10", "part11", "part12", "part13", "part14"];

		string key = utility.Create<CacheKeyUtilityTest>(keyParts);

		Assert.Equal($"{typeof(CacheKeyUtilityTest).FullName}:{string.Join(":", keyParts)}".ToUpperInvariant(), key);
	}

	private static CacheKeyUtility CreateCacheKeyUtility()
	{
		IDataLookupNormalizer lookupNormalizer = CoreUtilitiesMocks.CreateILookupNormalizer();
		return new CacheKeyUtility(new Mock<ILogger<CacheKeyUtility>>().Object, lookupNormalizer);
	}
}
