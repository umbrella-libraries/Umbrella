using System.Xml.Linq;
using Umbrella.Utilities.Extensions;

namespace Umbrella.Utilities.Test.Extensions;

public class XElementExtensionsTest
{
	[Fact]
	public void GetAttributeValue_T_AllValid()
	{
		XElement element = CreateXElement();

		Assert.True(element.GetAttributeValue<bool>("bool"));

		Assert.Equal<byte>(10, element.GetAttributeValue<byte>("byte"));
		Assert.Equal<short>(20, element.GetAttributeValue<short>("short"));
		Assert.Equal(100, element.GetAttributeValue<int>("int"));
		Assert.Equal(200, element.GetAttributeValue<long>("long"));
		Assert.Equal(10.2f, element.GetAttributeValue<float>("float"));
		Assert.Equal(100.2, element.GetAttributeValue<double>("double"));
		Assert.Equal(1000.2m, element.GetAttributeValue<decimal>("decimal"));

		Assert.Equal(200.2, element.GetAttributeValue<double?>("nullable-double"));
		Assert.Null(element.GetAttributeValue<double?>("nullable-double-empty"));
		Assert.Null(element.GetAttributeValue<double?>("nullable-double-whitespace"));
	}

	private static XElement CreateXElement()
		=> new("test",
			new XAttribute("bool", true),
			new XAttribute("byte", 10),
			new XAttribute("short", 20),
			new XAttribute("int", 100),
			new XAttribute("long", 200),
			new XAttribute("float", 10.2f),
			new XAttribute("double", 100.2),
			new XAttribute("decimal", 1000.2),
			new XAttribute("nullable-double", 200.2),
			new XAttribute("nullable-double-empty", ""),
			new XAttribute("nullable-double-whitespace", "         "));

}