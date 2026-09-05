namespace Umbrella.AspNetCore.Blazor.Components.DynamicImage;

internal static class DynamicImageParameterValidation
{
	internal static void Validate(ParameterView parameters)
	{
		if (!parameters.TryGetValue<object>("Image", out _))
			return;
		foreach (ParameterValue parameter in parameters)
		{
			if (parameter.Name is "Url" or "VersionToken" or "FocalPointX" or "FocalPointY" or "FocalPointApproval")
				throw new InvalidOperationException("Supply Image or individual image metadata parameters, not both.");
		}
	}
}
